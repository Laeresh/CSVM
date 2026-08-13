using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The D11 mode machine's transition table, engine-free: the nine decoded modes under fixed
/// rolls (chances pinned to 0/1) and seeded rngs. Pins activation into pursue, the return-range
/// exit, the steady-hand and sixth-sense reactions in the engine's own vocabulary, the evasive
/// maneuver playing to Done and returning, the avoid-crash override on an injected probe, the
/// signature-maneuver weighting, and seed determinism.
/// </summary>
public class AiModeMachineTests
{
    private static readonly Vector3 Home = new(0f, 400f, 0f);
    private static readonly Vector3 Level = new(0f, 0f, -100f); // 100 m/s along -Z

    [Fact]
    public void ModeNamesAreTheEngineVocabulary()
    {
        Assert.Equal("patrol", AiModeMachine.NameOf(AiMode.Patrol));
        Assert.Equal("pursue", AiModeMachine.NameOf(AiMode.Pursue));
        Assert.Equal("lay off", AiModeMachine.NameOf(AiMode.LayOff));
        Assert.Equal("evade", AiModeMachine.NameOf(AiMode.Evade));
        Assert.Equal("evasive maneuver", AiModeMachine.NameOf(AiMode.EvasiveManeuver));
        Assert.Equal("stunned", AiModeMachine.NameOf(AiMode.Stunned));
        Assert.Equal("avoid crash", AiModeMachine.NameOf(AiMode.AvoidCrash));
        Assert.Equal("approaching danger zone", AiModeMachine.NameOf(AiMode.ApproachingDangerZone));
        Assert.Equal("navigating danger zone", AiModeMachine.NameOf(AiMode.NavigatingDangerZone));
    }

    [Fact]
    public void PatrolActivatesIntoPursueInsideTheRadius()
    {
        var m = Machine();
        var transitions = new List<(AiMode From, AiMode To)>();
        m.ModeChanged += (from, to, _) => transitions.Add((from, to));

        // Outside min_ai_active_dist / attack (both 2000 shipped): stays on patrol.
        Assert.Equal(AiMode.Patrol, m.Update(Home, Level, Home + new Vector3(2500f, 0f, 0f), null, 0.1f));
        Assert.Empty(transitions);

        // Inside: pursue, announced as one patrol -> pursue transition.
        Assert.Equal(AiMode.Pursue, m.Update(Home, Level, Home + new Vector3(1900f, 0f, 0f), null, 0.1f));
        Assert.Equal((AiMode.Patrol, AiMode.Pursue), Assert.Single(transitions));
    }

    [Fact]
    public void PursuitEndsOnTargetLossAndPerTheReturnLeash()
    {
        // Target lost: straight back to patrol.
        var m = Machine();
        PursueFrom(m, Home + new Vector3(1500f, 0f, 0f));
        Assert.Equal(AiMode.Patrol, m.Update(Home, Level, null, null, 0.1f));

        // The leash (our reading of return_range): beyond ReturnRange from where the pursuit
        // began AND the target outside the activation radius.
        var m2 = Machine();
        PursueFrom(m2, Home + new Vector3(1500f, 0f, 0f));
        var strayed = Home + new Vector3(1300f, 0f, 0f); // > 1200 from the anchor
        var farTarget = Home + new Vector3(4000f, 0f, 0f); // > 2000 from the plane
        Assert.Equal(AiMode.Patrol, m2.Update(strayed, Level, farTarget, null, 0.1f));

        // Still inside the leash: the far target alone does not end the chase.
        var m3 = Machine();
        PursueFrom(m3, Home + new Vector3(1500f, 0f, 0f));
        Assert.Equal(AiMode.Pursue, m3.Update(Home + new Vector3(900f, 0f, 0f), Level, farTarget, null, 0.1f));
    }

    [Fact]
    public void FailedSteadyHandEvadesAndAPassedOneDoesNot()
    {
        // Chance pinned to 1: the roll always fails, in the decoded vocabulary.
        var m = Machine();
        string? logged = null;
        m.RollLogged += line => logged = line;
        PursueFrom(m, Home + new Vector3(500f, 0f, 0f));
        m.NotifyDamage(12f, new Vector3(1f, 0f, 0f));
        m.SteadyHandChance = 1f;
        m.NotifyDamage(12f, new Vector3(1f, 0f, 0f));
        Assert.Equal(AiMode.Evade, m.Mode);
        Assert.Contains("steady hand test failed. Evading.", logged);
        Assert.Contains("absorbed 12.0 damage", logged);

        // Chance pinned to 0: the roll always passes and nothing moves.
        var m2 = Machine();
        string? logged2 = null;
        m2.RollLogged += line => logged2 = line;
        PursueFrom(m2, Home + new Vector3(500f, 0f, 0f));
        m2.SteadyHandChance = 0f;
        m2.NotifyDamage(5f, new Vector3(1f, 0f, 0f));
        Assert.Equal(AiMode.Pursue, m2.Mode);
        Assert.Contains("steady hand test passed. Not evading.", logged2);
    }

    [Fact]
    public void EvadeScramblesItsCourseAndReturnsToThePriorMode()
    {
        var m = Machine();
        var target = Home + new Vector3(500f, 0f, 0f);
        PursueFrom(m, target);
        m.SteadyHandChance = 1f;
        m.NotifyDamage(8f, new Vector3(1f, 0f, 0f));
        Assert.Equal(AiMode.Evade, m.Mode);
        float firstHeading = m.EvadeHeadingDeg;

        // The invented scramble: past the interval the ordered heading has moved.
        for (int i = 0; i < (int)(AiModeMachine.EvadeScrambleIntervalS * 60f) + 5; i++)
            m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Evade, m.Mode);
        Assert.NotEqual(firstHeading, m.EvadeHeadingDeg);

        // The run expires with the target still in range: back to pursue.
        for (int i = 0; i < (int)(AiModeMachine.EvadeDurationS * 60f); i++)
            m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Pursue, m.Mode);

        // Same reaction with the target gone by the end: patrol instead.
        var m2 = Machine();
        PursueFrom(m2, target);
        m2.SteadyHandChance = 1f;
        m2.NotifyDamage(8f, new Vector3(1f, 0f, 0f));
        for (int i = 0; i < (int)(AiModeMachine.EvadeDurationS * 60f) + 10; i++)
            m2.Update(Home, Level, null, null, 1f / 60f);
        Assert.Equal(AiMode.Patrol, m2.Mode);
    }

    [Fact]
    public void FailedSixthSenseStunsAndRecoversAfterTheInterval()
    {
        var m = Machine();
        string? logged = null;
        m.RollLogged += line => logged = line;
        var target = Home + new Vector3(500f, 0f, 0f);
        PursueFrom(m, target);
        m.SixthSenseChance = 0f; // the pass roll always fails
        m.StunRecoveryIntervalS = 1.5f;

        // The in-machine trigger: the pursued AI target breaks into an evasive state.
        m.Update(Home, Level, target, AiMode.EvasiveManeuver, 1f / 60f);
        Assert.Equal(AiMode.Stunned, m.Mode);
        Assert.Contains("Sixth sense test failed; AI now stunned.", logged);

        // Nothing moves the mode during the stun; it recovers to the prior mode after the
        // interval.
        for (int i = 0; i < (int)(1.5f * 60f) - 5; i++)
            Assert.Equal(AiMode.Stunned, m.Update(Home, Level, target, null, 1f / 60f));
        for (int i = 0; i < 15; i++)
            m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Pursue, m.Mode);

        // A passed roll is logged and does not stun.
        var m2 = Machine();
        string? logged2 = null;
        m2.RollLogged += line => logged2 = line;
        PursueFrom(m2, target);
        m2.SixthSenseChance = 1f;
        m2.NotifyTargetEvaded();
        Assert.Equal(AiMode.Pursue, m2.Mode);
        Assert.Contains("Sixth sense test passed.", logged2);
    }

    [Fact]
    public void EvasiveManeuverPlaysAnEligibleEntryToDoneAndReturns()
    {
        var m = Machine();
        m.NaturalTouch = 2;
        m.Library = new[] { QuickManeuver("bank_turn", 2), QuickManeuver("split_s", 8), Stub("high_yo_yo") };
        var target = Home + new Vector3(500f, 0f, 0f);
        PursueFrom(m, target);
        m.SteadyHandChance = 1f;
        string? reason = null;
        m.ModeChanged += (_, to, why) => { if (to == AiMode.EvasiveManeuver) reason = why; };
        m.NotifyDamage(8f, new Vector3(1f, 0f, 0f));

        // Only the entry inside the natural-touch cull is playable; the stub never is.
        Assert.Equal(AiMode.EvasiveManeuver, m.Mode);
        Assert.NotNull(m.Executor);
        Assert.Equal("bank_turn", m.Executor!.Maneuver.Name);
        Assert.Contains("natural touch 2/2", reason);

        // Play the program out (the owner steps the executor; the machine watches Done).
        var model = new FlightModel(new PlaneStats());
        for (int i = 0; i < 20 && !m.Executor!.Done; i++)
            m.Executor.Next(model, 0.02f);
        Assert.True(m.Executor!.Done);
        Assert.Equal(AiMode.Pursue, m.Update(Home, Level, target, null, 1f / 60f));
        Assert.Null(m.Executor);
    }

    [Fact]
    public void SignatureManeuversAreWeightedUpInTheDraw()
    {
        int signaturePicks = 0;
        const int runs = 200;
        for (int seed = 0; seed < runs; seed++)
        {
            var m = Machine(seed);
            m.NaturalTouch = 9;
            m.Library = new[] { QuickManeuver("loop", 7), QuickManeuver("split_s", 8) };
            m.SignatureManeuvers = new[] { "split_s" };
            PursueFrom(m, Home + new Vector3(500f, 0f, 0f));
            m.SteadyHandChance = 1f;
            m.NotifyDamage(8f, new Vector3(1f, 0f, 0f));
            if (m.Executor!.Maneuver.Name == "split_s")
                signaturePicks++;
        }

        // Weight 3 against 1: the expectation is 3/4 of the draws; well above an even split.
        Assert.InRange(signaturePicks, (int)(runs * 0.60), runs);
    }

    [Fact]
    public void AvoidCrashOverridesUntilTheProbeClears()
    {
        var m = Machine();
        bool blocked = false;
        m.ProbeBlocked = (_, _) => blocked;

        // Clear probes: patrol undisturbed.
        Assert.Equal(AiMode.Patrol, m.Update(Home, Level, null, null, AiModeMachine.ProbeIntervalS));

        // Terrain inside the lookahead: the override takes the mode and orders a climb-out.
        blocked = true;
        Assert.Equal(AiMode.AvoidCrash, m.Update(Home, Level, null, null, AiModeMachine.ProbeIntervalS));
        Assert.Equal(Home.Y + AiModeMachine.ClimbOutM, m.ClimbOutAltitude, 3);

        // Clear again: released after the clear-streak, back to the prior mode.
        blocked = false;
        for (int i = 0; i <= AiModeMachine.ClearProbesToExit; i++)
            m.Update(Home, Level, null, null, AiModeMachine.ProbeIntervalS);
        Assert.Equal(AiMode.Patrol, m.Mode);
    }

    [Fact]
    public void FixedSeedsTransitionIdentically()
    {
        AiModeMachine Build() // real (0..1) chances so the rolls are genuinely random draws
        {
            var m = new AiModeMachine(new Random(7))
            {
                SteadyHandChance = 0.5f,
                SixthSenseChance = 0.5f,
                StunRecoveryIntervalS = 1f,
            };
            return m;
        }

        List<AiMode> Run(AiModeMachine m)
        {
            var target = Home + new Vector3(600f, 0f, 0f);
            var seen = new List<AiMode> { m.Mode };
            m.ModeChanged += (_, to, _) => seen.Add(to);
            m.Update(Home, Level, target, null, 1f / 60f);
            for (int i = 0; i < 600; i++)
            {
                if (i % 90 == 0)
                    m.NotifyDamage(6f, new Vector3(1f, 0f, 0f));
                if (i % 240 == 120)
                    m.NotifyTargetEvaded();
                m.Update(Home, Level, target, null, 1f / 60f);
            }
            return seen;
        }

        Assert.Equal(Run(Build()), Run(Build()));
    }

    private static AiModeMachine Machine(int seed = 1) => new(new Random(seed));

    /// <summary>A one-step maneuver the executor finishes in a fraction of a second.</summary>
    private static Maneuver QuickManeuver(string name, int difficulty, float duration = 0.05f) =>
        new()
        {
            Name = name,
            Difficulty = difficulty,
            Steps = new[] { new ManeuverStep(duration, 10f, 0f, 0f, Array.Empty<float>()) },
        };

    private static Maneuver Stub(string name) => new()
    {
        Name = name,
        Difficulty = 99,
        Steps = Array.Empty<ManeuverStep>(),
    };

    private static AiMode PursueFrom(AiModeMachine m, Vector3 target)
    {
        var mode = m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Pursue, mode);
        return mode;
    }
}
