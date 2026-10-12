using System;
using CSVM.Effects;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The track a hull's wake rides on the wave ocean (<see cref="OceanWakeTrack"/>). Its samples fall on
/// whole multiples of the sample interval of sim time, whatever the frame pacing. A standing hull writes
/// no wake, and two runs over the same sim times write the same uniforms.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class OceanWakeTrackTests
{
    private const int Samples = OceanShader.WakeSamples;
    private static readonly Vector2 Half = new(40f, 8f);
    private static readonly Vector2 Axis = new(0.6f, -0.8f);

    [Fact]
    public void AStandingHullDrawsNoWake()
    {
        var track = new OceanWakeTrack();
        var at = new Vector2(-5000f, 2000f);
        for (int i = 0; i <= 60 * 30; i++)
            track.Observe(i / 60.0, at);
        var buffer = new Vector4[Samples];
        Assert.Equal(OceanWakeTrack.Unused, track.Write(buffer, 0, Half, Axis));
        Assert.True(track.Count > 0, "the samples are taken; they only stand in one place");
    }

    [Fact]
    public void AMovingHullWritesItsPathNewestFirst()
    {
        var track = new OceanWakeTrack();
        for (int i = 0; i <= 60 * 10; i++)
            track.Observe(i / 60.0, Straight(i / 60.0));
        var buffer = new Vector4[Samples];
        // The axis either way along it: the lead still runs back against the travel.
        var bounds = track.Write(buffer, 0, Half, -Axis);
        // At 10 s: the hull, the lead three quarters of the way to 8 s, then 8 s and 4 s, the last repeated.
        Assert.Equal(Straight(10.0), Xz(buffer[0]), Vec(1e-3f));
        Assert.Equal(Straight(8.5), Xz(buffer[1]), Vec(1e-2f));
        Assert.Equal(Straight(8.0), Xz(buffer[2]), Vec(1e-3f));
        Assert.Equal(Straight(4.0), Xz(buffer[3]), Vec(1e-3f));
        Assert.Equal(new[] { 0f, 18f, 24f, 72f }, new[] { buffer[0].Z, buffer[1].Z, buffer[2].Z, buffer[3].Z }, Vec(1e-2f));
        Assert.Equal(new[] { 0f, 1.5f, 2f, 6f }, new[] { buffer[0].W, buffer[1].W, buffer[2].W, buffer[3].W }, Vec(1e-2f));
        for (int k = 4; k < Samples; k++)
            Assert.Equal(buffer[3], buffer[k]);
        Assert.Equal(72f, bounds.W, 1e-2f);
        Assert.True(bounds.Z > 36f, $"the reach covers the track ({bounds.Z})");
    }

    [Fact]
    public void FramePacingMovesNoSample()
    {
        var steady = Run(t => Turning(t), 60 * 40, i => i / 60.0);
        var ragged = Run(t => Turning(t), 37 * 40, i => (i / 37.0) + (0.004 * Math.Sin(i * 1.7)));
        // The lead hangs off the current position, which the ragged run reaches a few ms off.
        Assert.Equal(Xz(steady[1]), Xz(ragged[1]), Vec(0.1f));
        for (int k = 2; k < Samples; k++)
        {
            Assert.Equal(Xz(steady[k]), Xz(ragged[k]), Vec(0.02f));
            Assert.Equal(steady[k].W, ragged[k].W, 0.1f);
        }
    }

    [Fact]
    public void TwoRunsOverTheSameSimTimesMatchBitForBit()
    {
        var first = Run(t => Turning(t), 60 * 25, i => i / 60.0);
        var second = Run(t => Turning(t), 60 * 25, i => i / 60.0);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(20.0, 0f)]
    [InlineData(0.05, 500f)]
    public void AGapOrAJumpStartsTheTrackAgain(double gap, float jump)
    {
        var track = new OceanWakeTrack();
        for (int i = 0; i <= 60 * 12; i++)
            track.Observe(i / 60.0, Straight(i / 60.0));
        double t = 12.0 + gap;
        track.Observe(t, Straight(t) + new Vector2(jump, 0f));
        Assert.Equal(0, track.Count);
        Assert.Equal(OceanWakeTrack.Unused, track.Write(new Vector4[Samples], 0, Half, Axis));
    }

    [Fact]
    public void AStopLeavesTheTrailToAge()
    {
        var track = new OceanWakeTrack();
        int i = 0;
        for (; i <= 60 * 12; i++)
            track.Observe(i / 60.0, Straight(i / 60.0));
        var stop = Straight(12.0);
        for (; i <= 60 * 20; i++)
            track.Observe(i / 60.0, stop);
        var buffer = new Vector4[Samples];
        var bounds = track.Write(buffer, 0, Half, Axis);
        Assert.True(bounds.W > 0f, "the wake behind a hull that stopped is still drawn, older");
        Assert.Equal(stop, Xz(buffer[0]), Vec(1e-3f));
        Assert.True(buffer[1].W >= 8f, $"the newest kept sample dates from before the stop ({buffer[1].W})");
    }

    [Fact]
    public void TheLeadFollowsTheHeadingAfterATurn()
    {
        // East at 10 m/s to 9 s, then hard north: the newest sample still lies on the eastward leg.
        var track = new OceanWakeTrack();
        static Vector2 Path(double t) => t <= 9.0 ? new Vector2((float)(10.0 * t), 0f) : new Vector2(90f, (float)(10.0 * (t - 9.0)));
        for (int i = 0; i <= 60 * 11; i++)
            track.Observe(i / 60.0, Path(i / 60.0));
        var buffer = new Vector4[Samples];
        track.Write(buffer, 0, Half, new Vector2(0f, 1f));
        Assert.Equal(new Vector2(90f, 20f), Xz(buffer[0]), Vec(1e-3f));
        Assert.Equal(90f, buffer[1].X, 1e-3f);
        Assert.True(buffer[1].Y < 20f && buffer[1].Y > 0f, $"the lead runs back south along the new heading ({buffer[1]})");
    }

    // 12 m/s along a heading off the axes, from a far world position.
    private static Vector2 Straight(double t) => new Vector2(-5200f, 9800f) + (new Vector2(0.6f, -0.8f) * (float)(12.0 * t));

    // 10 m/s on a 300 m circle.
    private static Vector2 Turning(double t)
    {
        double a = 10.0 * t / 300.0;
        return new Vector2(-5200f + (float)(300.0 * Math.Cos(a)), 9800f + (float)(300.0 * Math.Sin(a)));
    }

    private static Vector4[] Run(Func<double, Vector2> path, int frames, Func<int, double> time)
    {
        var track = new OceanWakeTrack();
        for (int i = 0; i <= frames; i++)
            track.Observe(time(i), path(time(i)));
        var buffer = new Vector4[Samples];
        double end = time(frames);
        track.Write(buffer, 0, Half, (path(end) - path(end - 0.01)).Normalized());
        return buffer;
    }

    private static Vector2 Xz(Vector4 v) => new(v.X, v.Y);

    private static Comparer Vec(float eps) => new(eps);

    private sealed class Comparer : System.Collections.Generic.IEqualityComparer<Vector2>, System.Collections.Generic.IEqualityComparer<float>
    {
        private readonly float _eps;

        public Comparer(float eps) => _eps = eps;

        public bool Equals(Vector2 a, Vector2 b) => a.DistanceTo(b) <= _eps;

        public bool Equals(float a, float b) => Math.Abs(a - b) <= _eps;

        public int GetHashCode(Vector2 v) => 0;

        public int GetHashCode(float v) => 0;
    }
}
