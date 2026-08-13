using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The F17 zeppelin motion law, engine-free: the decoded sqrt engine-loss curve (never the
/// design's 10/40/50 bands), the record's rate/speed/pitch limits enforced per step, the
/// verbatim (unclamped) initial pitch, and a net flown with every hop on the edge list.
/// </summary>
public class ZeppelinMotionTests
{
    private const float Dt = 1f / 60f;

    [Theory]
    [InlineData(4, 4, 1f)]
    [InlineData(2, 4, 0.70710678f)]
    [InlineData(1, 4, 0.5f)]
    [InlineData(0, 4, 0f)]
    public void TheEngineLossCurveIsTheDecodedSquareRoot(int alive, int total, float expected)
    {
        Assert.Equal(expected, ZeppelinMotion.EngineFactor(alive, total), 5);
    }

    [Fact]
    public void EngineFactorClampsOutOfRangeCounts()
    {
        Assert.Equal(1f, ZeppelinMotion.EngineFactor(9, 4));
        Assert.Equal(0f, ZeppelinMotion.EngineFactor(-1, 4));
        Assert.Equal(1f, ZeppelinMotion.EngineFactor(0, 0)); // no engines list: unscaled
    }

    [Fact]
    public void EngineLossScalesSpeedToZeroButAccelKeepsTheTwentyPercentFloor()
    {
        var m = NewMotion(Def(), SquareNet());
        Assert.Equal(4, m.TotalEngines);
        Assert.Equal(15f, m.EffectiveMaxSpeed);

        m.AliveEngines = 2;   // THE F18 SEAM: the damage side writes the surviving count
        Assert.Equal(15f * 0.70710678f, m.EffectiveMaxSpeed, 3);
        Assert.Equal(((0.8f * 0.70710678f) + 0.2f) * 4.47f, m.EffectiveMaxAccel, 3);

        m.AliveEngines = 0;
        Assert.Equal(0f, m.EffectiveMaxSpeed);
        Assert.Equal(0.2f * 4.47f, m.EffectiveMaxAccel, 4);
    }

    [Fact]
    public void TotalEngineLossDeceleratesTheZeppelinToAStop()
    {
        var m = NewMotion(Def(), SquareNet());
        for (int i = 0; i < 60 * 30 && m.Speed < 14.9f; i++)
        {
            m.Step(Dt);
        }
        Assert.True(m.Speed > 14.5f, $"cruise never reached: {m.Speed}");

        m.AliveEngines = 0;
        float before = m.Speed;
        m.Step(Dt);
        Assert.True(m.Speed < before, "no deceleration after total engine loss");
        for (int i = 0; i < 60 * 60 && m.Speed > 0f; i++)
        {
            m.Step(Dt);
        }
        Assert.Equal(0f, m.Speed);
    }

    [Fact]
    public void SpeedAndAccelerationStayInsideTheRecordsLimits()
    {
        var m = NewMotion(Def(), SquareNet());
        float last = 0f;
        for (int i = 0; i < 60 * 60; i++)
        {
            m.Step(Dt);
            Assert.True(m.Speed <= m.Def.MaxSpeed + 1e-3f, $"speed {m.Speed} over max");
            Assert.True(Math.Abs(m.Speed - last) <= (m.Def.MaxAccel * Dt) + 1e-4f,
                $"accel over max at step {i}");
            last = m.Speed;
        }
    }

    [Fact]
    public void TheYawRateNeverExceedsMaxRateYaw()
    {
        // Spawn facing away from the first node so the law has a big turn to make.
        var def = Def(yawDeg: 180f);
        var m = NewMotion(def, SquareNet());
        float maxRate = Mathf.DegToRad(def.MaxRateYawDeg);
        float last = m.YawRad;
        for (int i = 0; i < 60 * 60; i++)
        {
            m.Step(Dt);
            float delta = Mathf.Abs(Mathf.AngleDifference(last, m.YawRad));
            Assert.True(delta <= (maxRate * Dt) + 1e-4f, $"yaw rate over limit at step {i}");
            last = m.YawRad;
        }
    }

    [Fact]
    public void ThePitchBandHoldsOnASteepClimbTarget()
    {
        // A climb of 2000 m over 2000 m horizontal asks for ~45° of pitch; the band caps the
        // command at +30° (and the bounce back down at −30°).
        var net = Net(new Vector3(0f, 400f, 0f), new Vector3(0f, 2400f, -2000f));
        var m = NewMotion(Def(), net);
        float maxPitch = Mathf.DegToRad(30f);
        for (int i = 0; i < 60 * 120; i++)
        {
            m.Step(Dt);
            Assert.True(m.PitchRad <= maxPitch + 1e-4f, $"pitch over band at step {i}");
            Assert.True(m.PitchRad >= -maxPitch - 1e-4f, $"pitch under band at step {i}");
        }
        Assert.True(m.Position.Y > 500f, $"never climbed: y={m.Position.Y}");
    }

    [Fact]
    public void TheAuthoredInitialPitchIsTakenVerbatimNeverLoadClamped()
    {
        // The original's load-time clamp compares radians against raw degrees and never fires
        // (the unit bug); we deliberately do not reproduce it as a working clamp, so an
        // out-of-band authored pitch arrives verbatim.
        var m = NewMotion(Def(pitchDeg: 45f), SquareNet());
        Assert.Equal(Mathf.DegToRad(45f), m.PitchRad, 5);
    }

    [Fact]
    public void TheNetIsFlownNodeToNodeAlongTheEdgeList()
    {
        var net = SquareNet();
        var follower = new AiNetFollower(net, new Random(1), 250f);
        var m = new ZeppelinMotion(Def(), follower);
        var hops = new List<(int From, int To)>();
        int last = -1;
        int steps = 0;
        while (follower.Advances < 4 && steps < 60 * 600)
        {
            steps++;
            m.Step(Dt);
            if (follower.CurrentIndex != last)
            {
                if (last >= 0)
                {
                    hops.Add((last, follower.CurrentIndex));
                }
                last = follower.CurrentIndex;
            }
        }
        Assert.True(follower.Advances >= 4, $"only {follower.Advances} captures in {steps} steps");
        foreach (var (from, to) in hops)
        {
            Assert.True(net.Edges.Contains((from, to)) || net.Edges.Contains((to, from)),
                $"hop {from}->{to} is not an edge");
        }
    }

    private static ZeppelinDef Def(float yawDeg = 0f, float pitchDeg = 0f) => new()
    {
        Node = "testzep",
        Position = new Vector3(0f, 400f, 300f),
        YawDeg = yawDeg,
        PitchDeg = pitchDeg,
        MaxSpeed = 15f,
        MaxAccel = 4.47f,
        AccelPitchDeg = 0.5f,
        AccelYawDeg = 1.5f,
        MaxRateYawDeg = 15f,
        MaxRatePitchDeg = 5f,
        MinPitchDeg = -30f,
        MaxPitchDeg = 30f,
        Net = "TestNet",
        Targets = Array.Empty<string>(),
        Healthy = Array.Empty<ZeppelinHealthyZone>(),
        NumHealthyRequired = 1,
        Engines = new[] { "leng1", "leng2", "reng1", "reng2" },
        Gasbags = Array.Empty<ZeppelinGasbag>(),
        LeftCannons = Array.Empty<ZeppelinCannon>(),
        RightCannons = Array.Empty<ZeppelinCannon>(),
        CannonHealth = Array.Empty<ZeppelinCannonHealth>(),
    };

    private static ZeppelinMotion NewMotion(ZeppelinDef def, AiNet net) =>
        new(def, new AiNetFollower(net, new Random(1), 250f));

    // A 1 km square patrol ring at the def's altitude.
    private static AiNet SquareNet() => Net(
        new Vector3(0f, 400f, 0f), new Vector3(1000f, 400f, 0f),
        new Vector3(1000f, 400f, 1000f), new Vector3(0f, 400f, 1000f));

    private static AiNet Net(params Vector3[] positions)
    {
        var nodes = new List<AiNetNode>();
        foreach (var p in positions)
        {
            nodes.Add(new AiNetNode(p, Array.Empty<float>()));
        }
        var edges = new List<(int A, int B)>();
        for (int i = 0; i < nodes.Count; i++)
        {
            edges.Add((i, (i + 1) % nodes.Count));
        }
        if (nodes.Count == 2)
        {
            edges.RemoveAt(1); // a two-node net needs one edge, not a doubled pair
        }
        return new AiNet { Id = 1, Name = "TestNet", Nodes = nodes, Edges = edges };
    }
}
