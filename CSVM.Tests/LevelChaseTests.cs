using System;
using System.Collections.Generic;
using CSVM.Flight.Camera;
using Godot;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// The flat level chase's frame (<see cref="LevelChase"/>) over scripted attitudes at 60 steps a
/// second. They are turns, loops, a roll upright, vertical flight and a held knife-edge. Every
/// run checks the frame keeps the aircraft's forward and never turns more in a step than the
/// aircraft's nose did plus the capped roll.
/// </summary>
public class LevelChaseTests
{
    private const float Dt = 1f / 60f;

    // The horizontal tolerance on the frame's right axis, as its world height (a sine).
    private const float Flat = 1e-4f;

    // Rates the net suites pose aerobatics at: half loops at 90 deg/s, rolls at 180 deg/s.
    private static readonly float PitchRate = Mathf.DegToRad(90f);
    private static readonly float RollRate = Mathf.DegToRad(180f);

    private readonly ITestOutputHelper _out;

    public LevelChaseTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AHeldSixtyDegreeTurnNeverBanksTheFrame()
    {
        // A right turn at 60 deg of bank, entered at the roll rate and held for ten seconds.
        var run = Fly(Steps(10f, t => Heading(-0.35f * t) * Bank(-Mathf.Min(RollRate * t, Mathf.DegToRad(60f)))));
        run.AssertFlat(0, run.Frames.Count, _out, "60 deg turn");
        Assert.True(run.Frames[^1].Y.Y > 0.999f, "upright");
    }

    [Fact]
    public void ASteepTurnAtEightyFiveDegreesStaysLevel()
    {
        var run = Fly(Steps(6f, t => Heading(-0.6f * t) * Bank(-Mathf.Min(RollRate * t, Mathf.DegToRad(85f)) + (0.05f * Mathf.Sin(5f * t)))));
        run.AssertFlat(0, run.Frames.Count, _out, "85 deg turn");
        Assert.True(run.Frames[^1].Y.Y > 0.99f, "upright");
    }

    [Fact]
    public void AFullLoopFollowsTheAircraftOverTheTop()
    {
        var run = Fly(Steps(4f, t => Heading(0.7f) * Pitch(PitchRate * t)));
        run.AssertFlat(0, run.Frames.Count, _out, "loop");
        for (int i = 0; i < run.Frames.Count; i++)
        {
            // Wings level all the way round, so the frame's up is the aircraft's own.
            Assert.True(run.Frames[i].Y.Dot(run.Attitudes[i].Y) > 0.9999f, $"step {i}: up {run.Frames[i].Y}");
        }

        // The top of the loop is drawn upside down.
        Assert.True(run.Frames[120].Y.Y < -0.999f, $"top: up {run.Frames[120].Y}");
    }

    [Fact]
    public void ALoopBesideVerticalCarriesTheRightThroughTheCone()
    {
        // The nose passes about 3 deg beside vertical, so the level right spins about it there.
        var run = Fly(Steps(4f, t => Heading(0.7f) * Pitch(PitchRate * t) * Heading(0.05f)));
        float worstHeight = 0f;
        for (int i = 0; i < run.Frames.Count; i++)
        {
            worstHeight = Mathf.Max(worstHeight, Mathf.Abs(run.Frames[i].X.Y));
        }

        // The carried right tilts as the nose sweeps the cone (5.8 deg here), and rolls back to
        // level at the cap on leaving it.
        _out.WriteLine(Inv($"loop beside vertical: right axis tilt at most {Mathf.RadToDeg(Mathf.Asin(worstHeight)):0.##} deg, largest step {Mathf.RadToDeg(run.WorstTurn):0.###} deg"));
        Assert.True(worstHeight < Mathf.Sin(Mathf.DegToRad(6f)), $"right axis height {worstHeight}");
        run.AssertFlat(80, 160, _out, "beside vertical, between the cones");
        Assert.True(run.Frames[^1].Y.Y > 0.999f, "upright after the loop");
    }

    [Fact]
    public void AHalfLoopThenARollUprightRollsTheFrameWithTheAircraft()
    {
        // An Immelmann: a half loop at 90 deg/s, a right roll of half a turn at 180 deg/s, then
        // two seconds level. The aircraft's roll is about its forward, so world -Z after the loop.
        float half = Mathf.Pi / PitchRate, roll = Mathf.Pi / RollRate;
        var run = Fly(Steps(half + roll + 2f, t => t < half
            ? Pitch(PitchRate * t)
            : Pitch(Mathf.Pi) * Bank(-Mathf.Min(RollRate * (t - half), Mathf.Pi))));
        int rollStart = (int)(half / Dt), settled = (int)((half + roll + 0.5f) / Dt);
        run.AssertFlat(0, rollStart, _out, "half loop");
        run.AssertFlat(settled, run.Frames.Count, _out, "after the roll");
        Assert.True(run.Frames[^1].Y.Y > 0.9999f, $"upright at the end: up {run.Frames[^1].Y}");

        // The frame rolls the aircraft's way only, never back.
        float back = 0f;
        for (int i = rollStart + 1; i < run.Frames.Count; i++)
        {
            float step = Mathf.Atan2(run.Frames[i - 1].X.Cross(run.Frames[i].X).Dot(-run.Frames[i].Z), run.Frames[i - 1].X.Dot(run.Frames[i].X));
            // A right roll turns the right axis positively about the forward.
            back = Mathf.Max(back, -step);
        }

        _out.WriteLine(Inv($"roll upright: largest step against the roll {Mathf.RadToDeg(back):0.####} deg"));
        Assert.True(back < 1e-4f, $"the frame rolled back {Mathf.RadToDeg(back)} deg in a step");
    }

    [Fact]
    public void ARollOnTheVerticalHoldsTheFrameAndLeavesItContinuous()
    {
        // Pitch to vertical, a full roll there, then over the top and down to level.
        float up = (Mathf.Pi / 2f) / PitchRate, spin = Mathf.Tau / RollRate;
        var run = Fly(Steps(up + spin + 3f, t => t < up
            ? Pitch(PitchRate * t)
            : t < up + spin
                ? Pitch(Mathf.Pi / 2f) * Bank(RollRate * (t - up))
                : Pitch((Mathf.Pi / 2f) + (PitchRate * (t - up - spin)))));
        int spinStart = (int)(up / Dt), spinEnd = (int)((up + spin) / Dt);
        for (int i = spinStart; i < spinEnd; i++)
        {
            Assert.True(run.Frames[i].X.IsEqualApprox(run.Frames[spinStart].X), $"step {i}: the right axis turned on the vertical");
        }

        run.AssertFlat(0, run.Frames.Count, _out, "vertical roll");
    }

    [Fact]
    public void AHeldKnifeEdgeKeepsTheLevelUntilTheBankPassesTheMargin()
    {
        // Rolled to 90 deg and held with a 5 deg wobble: the frame stays upright.
        var held = Fly(Steps(6f, t => Bank(-Mathf.Min(RollRate * t, Mathf.Pi / 2f) + (0.0873f * Mathf.Sin(4f * t)))));
        held.AssertFlat(0, held.Frames.Count, _out, "knife-edge");
        foreach (var frame in held.Frames)
        {
            Assert.True(frame.Y.Y > 0.9999f, $"up {frame.Y}");
        }

        // Within the margin at 105 deg it still holds; at 115 deg it rolls to the inverted level.
        Assert.True(Fly(Steps(3f, t => Bank(-Mathf.Min(RollRate * t, Mathf.DegToRad(105f))))).Frames[^1].Y.Y > 0.9999f);
        Assert.True(Fly(Steps(3f, t => Bank(-Mathf.Min(RollRate * t, Mathf.DegToRad(115f))))).Frames[^1].Y.Y < -0.9999f);
    }

    [Fact]
    public void AFreshShotSeedsTheLevelNearestTheAircraft()
    {
        var upright = LevelChase.Step(Bank(1.2f), null, Dt, out _);
        var inverted = LevelChase.Step(Bank(2.2f), null, Dt, out _);
        Assert.True(upright.Y.IsEqualApprox(Vector3.Up), $"up {upright.Y}");
        Assert.True(inverted.Y.IsEqualApprox(Vector3.Down), $"up {inverted.Y}");
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);

    private static Basis Heading(float rad) => new(Vector3.Up, rad);

    private static Basis Pitch(float rad) => new(Vector3.Right, rad);

    // Positive banks left (the right wing up), as Godot turns +X toward +Y about +Z.
    private static Basis Bank(float rad) => new(Vector3.Back, rad);

    private static IEnumerable<Basis> Steps(float seconds, Func<float, Basis> at)
    {
        for (int i = 0; i * Dt <= seconds; i++)
        {
            yield return at(i * Dt);
        }
    }

    private static Run Fly(IEnumerable<Basis> attitudes)
    {
        var run = new Run();
        LevelChaseHold? hold = null;
        foreach (var attitude in attitudes)
        {
            var frame = LevelChase.Step(attitude, hold, Dt, out var next);
            hold = next;
            Assert.True(frame.IsEqualApprox(frame.Orthonormalized()) && frame.Determinant() > 0.99f, "a rotation");
            Assert.True((-frame.Z).IsEqualApprox(-attitude.Orthonormalized().Z), "the aircraft's own forward");
            if (run.Frames.Count > 0)
            {
                // The frame turns no more than the nose did plus the roll cap (and float slack).
                float nose = (-run.Attitudes[^1].Z).AngleTo(-attitude.Z);
                float turn = run.Frames[^1].GetRotationQuaternion().AngleTo(frame.GetRotationQuaternion());
                Assert.True(turn <= nose + (LevelChase.RollRateRad * Dt) + 1e-3f,
                    $"step {run.Frames.Count}: the frame turned {Mathf.RadToDeg(turn):0.###} deg, the nose {Mathf.RadToDeg(nose):0.###}");
                run.WorstTurn = Mathf.Max(run.WorstTurn, turn);
            }

            run.Attitudes.Add(attitude);
            run.Frames.Add(frame);
        }

        return run;
    }

    private sealed class Run
    {
        public List<Basis> Attitudes { get; } = new();

        public List<Basis> Frames { get; } = new();

        public float WorstTurn { get; set; }

        public void AssertFlat(int from, int to, ITestOutputHelper output, string name)
        {
            float worst = 0f;
            for (int i = from; i < to; i++)
            {
                worst = Mathf.Max(worst, Mathf.Abs(Frames[i].X.Y));
            }

            output.WriteLine(Inv($"{name}: right axis height at most {worst:0.#######}, largest step {Mathf.RadToDeg(WorstTurn):0.###} deg"));
            Assert.True(worst < Flat, $"{name}: the right axis rose to {worst}");
        }
    }
}
