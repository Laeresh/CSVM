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
/// signature-maneuver weighting, the D15 lay-off entry/exit (a chasing human fallen behind,
/// gated by the AssistEnabled switch), and seed determinism.
/// </summary>
public class AiModeMachineTests
{
    // A dt that always leaves the per-plane probe timer due, whatever it drew from its rng.
    private const float Due = AiModeMachine.ProbeIntervalMaxS;

    private static readonly Vector3 Home = new(0f, 400f, 0f);
    private static readonly Vector3 Level = new(0f, 0f, -100f); // 100 m/s along -Z

    // Lay-off geometry: dead astern of a plane flying Level, and a velocity closing on it.
    private static readonly Vector3 Astern600 = Home + new Vector3(0f, 0f, 600f);
    private static readonly Vector3 Chasing = new(0f, 0f, -80f);

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

    /// <summary>The ordnance stun (a SONIC/FLASH burst, the smoke screen) enters the same
    /// state 4 the sixth-sense fail does, from ANY mode: the original writes the state without
    /// reading it, so a running maneuver or climb-out is dropped. It clears on its own and
    /// returns to the mode it interrupted.</summary>
    [Fact]
    public void AStunPreemptsAnyModeAndReturnsToTheInterruptedOne()
    {
        // From pursue: neutral for the given seconds, then pursue again.
        var m = Machine();
        var target = Home + new Vector3(500f, 0f, 0f);
        PursueFrom(m, target);
        string? reason = null;
        m.ModeChanged += (_, to, why) => { if (to == AiMode.Stunned) reason = why; };
        m.Stun(2f, "sonic burst");
        Assert.Equal(AiMode.Stunned, m.Mode);
        Assert.Equal("sonic burst", reason);
        Assert.Equal(2f, m.StunRemainingS, 3);
        for (int i = 0; i < 119; i++)
            Assert.Equal(AiMode.Stunned, m.Update(Home, Level, target, null, 1f / 60f));
        m.Update(Home, Level, target, null, 1f / 60f);
        m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Pursue, m.Mode);
        Assert.Equal(0f, m.StunRemainingS);

        // From an evasive maneuver: the executor is dropped, and the stun still returns to the
        // mode the maneuver itself would have returned to.
        var m2 = Machine();
        m2.NaturalTouch = 2;
        m2.Library = new[] { QuickManeuver("bank_turn", 2, duration: 5f) };
        PursueFrom(m2, target);
        m2.SteadyHandChance = 1f;
        m2.NotifyDamage(8f, new Vector3(1f, 0f, 0f));
        Assert.Equal(AiMode.EvasiveManeuver, m2.Mode);
        m2.Stun(0.5f);
        Assert.Equal(AiMode.Stunned, m2.Mode);
        Assert.Null(m2.Executor);
        for (int i = 0; i < 40; i++)
            m2.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Pursue, m2.Mode);

        // From avoid crash: the climb-out is overwritten too; with the line clear afterwards the
        // pilot is back on patrol, not stuck in either state.
        var m3 = Machine();
        bool blocked = true;
        m3.ProbeBlocked = (_, _) => blocked ? "test/obstacle" : null;
        Assert.Equal(AiMode.AvoidCrash, m3.Update(Home, Level, null, null, Due));
        m3.Stun(0.5f);
        Assert.Equal(AiMode.Stunned, m3.Mode);
        blocked = false;
        for (int i = 0; i < 40; i++)
            m3.Update(Home, Level, null, null, 1f / 60f);
        Assert.Equal(AiMode.Patrol, m3.Mode);

        // Zero or negative seconds is not a stun at all.
        var m4 = Machine();
        PursueFrom(m4, target);
        m4.Stun(0f);
        Assert.Equal(AiMode.Pursue, m4.Mode);
    }

    /// <summary>Re-entrancy is the design, not an accident: the original accepts its own stun
    /// state as an input state and OVERWRITES the expiry (clock + seconds, no max). The smoke
    /// screen holds a pilot by re-stunning every frame; a weaker hit shortens a longer stun.</summary>
    [Fact]
    public void AStunLandingOnAStunnedPilotOverwritesTheExpiry()
    {
        var m = Machine();
        var target = Home + new Vector3(500f, 0f, 0f);
        PursueFrom(m, target);
        m.Stun(5f);
        for (int i = 0; i < 60; i++)
            m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(4f, m.StunRemainingS, 2);

        // Refreshed every frame for a second: still the full interval left at the end of it.
        for (int i = 0; i < 60; i++)
        {
            m.Stun(5f);
            m.Update(Home, Level, target, null, 1f / 60f);
        }
        Assert.Equal(AiMode.Stunned, m.Mode);
        Assert.InRange(m.StunRemainingS, 4.9f, 5f);

        // A shorter stun on top does not extend; it replaces, and the pilot recovers on the
        // shorter clock.
        m.Stun(0.5f);
        Assert.Equal(0.5f, m.StunRemainingS, 3);
        for (int i = 0; i < 40; i++)
            m.Update(Home, Level, target, null, 1f / 60f);
        Assert.Equal(AiMode.Pursue, m.Mode);

        // The sixth-sense stun goes through the same entry, so it is refreshable too.
        var m2 = Machine();
        PursueFrom(m2, target);
        m2.SixthSenseChance = 0f;
        m2.StunRecoveryIntervalS = 1f;
        m2.NotifyTargetEvaded();
        Assert.Equal(AiMode.Stunned, m2.Mode);
        m2.Stun(3f);
        Assert.Equal(3f, m2.StunRemainingS, 3);
    }

    /// <summary>The stun cannot strand a pilot: an external mode override during it releases the
    /// controls, and a stunned pilot takes no reaction rolls that could pile a second timer on.</summary>
    [Fact]
    public void AStunnedPilotIsReleasedByAnOverrideAndTakesNoRolls()
    {
        var m = Machine();
        var target = Home + new Vector3(500f, 0f, 0f);
        PursueFrom(m, target);
        m.Stun(5f);
        m.SteadyHandChance = 1f;
        m.NotifyDamage(10f, new Vector3(1f, 0f, 0f));
        Assert.Equal(AiMode.Stunned, m.Mode); // no controls to break off with
        m.SixthSenseChance = 0f;
        m.NotifyTargetEvaded();
        Assert.Equal(AiMode.Stunned, m.Mode);

        m.Enter(AiMode.Patrol, "scripted");
        Assert.Equal(AiMode.Patrol, m.Mode);
        Assert.Equal(0f, m.StunRemainingS);
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
        m.ProbeBlocked = (_, _) => blocked ? "test/obstacle" : null;

        // Clear probes: patrol undisturbed.
        Assert.Equal(AiMode.Patrol, m.Update(Home, Level, null, null, Due));

        // Terrain inside the lookahead: the override takes the mode and orders a climb-out.
        blocked = true;
        Assert.Equal(AiMode.AvoidCrash, m.Update(Home, Level, null, null, Due));
        Assert.Equal(Home.Y + AiModeMachine.ClimbOutM, m.ClimbOutAltitude, 3);

        // Clear again: ONE clear ray releases it, in that same call. The original holds no
        // clear-streak (docs/org/aiPilot.md, "A clear ray releases the state in the same call").
        blocked = false;
        Assert.Equal(AiMode.Patrol, m.Update(Home, Level, null, null, Due));
    }

    [Fact]
    public void BelowTheFloorTheClimbOutArmsWithNoProbeAtAll()
    {
        var m = Machine();
        m.ProbeBlocked = (_, _) => null; // a clear line everywhere: only the floor can arm this

        var low = new Vector3(0f, AiModeMachine.AltitudeFloorM - 1f, 0f);
        Assert.Equal(AiMode.AvoidCrash, m.Update(low, Level, null, null, 0f));
        Assert.Equal(low.Y + AiModeMachine.ClimbOutM, m.ClimbOutAltitude, 3);

        // And back above it, the clear ray releases as usual.
        Assert.Equal(AiMode.Patrol, m.Update(Home, Level, null, null, Due));
    }

    [Fact]
    public void AboveTheCeilingNothingIsCastAndAClimbOutIsReleased()
    {
        var m = Machine();
        m.ProbeBlocked = (_, _) => "test/obstacle"; // blocked everywhere

        Assert.Equal(AiMode.AvoidCrash, m.Update(Home, Level, null, null, Due));

        // Above the ceiling the original runs no check and clears the state, blocked line or not.
        var high = new Vector3(0f, AiModeMachine.ProbeCeilingM + 1f, 0f);
        Assert.Equal(AiMode.Patrol, m.Update(high, Level, null, null, Due));
    }

    // ---- Lay off: the rubber-band assist ----------------------------------------------
    // Decoded: the mode and the sixth_sense_factor ease-off constant. Invented (named on the
    // machine's constants): the pursued-test cones, the enter/caught-up distances, the hold.

    [Fact]
    public void LayOffEntersWhenAChasingHumanFallsBehind()
    {
        var m = Machine();
        PursueFrom(m, Astern600);

        // One passing frame is NOT enough: the entry needs the geometry sustained (the
        // user-reported misfire regression — a turning fight satisfying the test momentarily).
        m.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true);
        Assert.Equal(AiMode.Pursue, m.Mode);

        SustainPursuit(m, Astern600, Chasing);
        Assert.Equal(AiMode.LayOff, m.Mode);

        // The lay-off course is the entry velocity's heading at the entry altitude.
        Assert.Equal(AiPilot.HeadingDegOf(Level), m.LayOffHeadingDeg, 3);
        Assert.Equal(Home.Y, m.LayOffAltitude, 3);

        // Inside the enter distance nothing moves: the pursuer has not fallen behind.
        var m2 = Machine();
        var astern300 = Home + new Vector3(0f, 0f, 300f);
        PursueFrom(m2, astern300);
        SustainPursuit(m2, astern300, Chasing);
        Assert.Equal(AiMode.Pursue, m2.Mode);

        // An interrupted window restarts the clock: geometry, a break, geometry again.
        var m3 = Machine();
        PursueFrom(m3, Astern600);
        for (int i = 0; i < 60; i++)
            m3.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true);
        m3.Update(Home, Level, Astern600, null, 1f / 60f, new Vector3(0f, 0f, 80f), targetIsHuman: true);
        for (int i = 0; i < 60; i++)
            m3.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true);
        Assert.Equal(AiMode.Pursue, m3.Mode); // 1 s + 1 s with a break never reaches 1.5 s

        // The nose axis outranks the velocity: nose pointed AT the target (a turn toward it)
        // reads as not-pursued even while the velocity still points away.
        var m4 = Machine();
        PursueFrom(m4, Astern600);
        int frames = (int)(AiModeMachine.LayOffSustainS * 60f) + 2;
        for (int i = 0; i < frames; i++)
            m4.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true,
                nose: new Vector3(0f, 0f, 1f)); // nose toward the target astern
        Assert.Equal(AiMode.Pursue, m4.Mode);
    }

    [Fact]
    public void LayOffNeverEntersWithoutTheAssistTheHumanOrTheChase()
    {
        // --no-assist's switch: same geometry, never entered.
        var m = Machine();
        m.AssistEnabled = false;
        PursueFrom(m, Astern600);
        SustainPursuit(m, Astern600, Chasing);
        Assert.Equal(AiMode.Pursue, m.Mode);

        // An AI pursuer gets no favours: the assist is for human players only.
        var m2 = Machine();
        PursueFrom(m2, Astern600);
        int frames = (int)(AiModeMachine.LayOffSustainS * 60f) + 2;
        for (int i = 0; i < frames; i++)
            m2.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: false);
        Assert.Equal(AiMode.Pursue, m2.Mode);

        // A target astern but flying AWAY is not pursuing.
        var m3 = Machine();
        PursueFrom(m3, Astern600);
        SustainPursuit(m3, Astern600, new Vector3(0f, 0f, 80f));
        Assert.Equal(AiMode.Pursue, m3.Mode);

        // A chasing target abeam is outside the rear cone.
        var m4 = Machine();
        var abeam = Home + new Vector3(600f, 0f, 0f);
        PursueFrom(m4, abeam);
        SustainPursuit(m4, abeam, new Vector3(-80f, 0f, 0f));
        Assert.Equal(AiMode.Pursue, m4.Mode);
    }

    [Fact]
    public void LayOffExitsWhenCaughtUpOrTheChaseEnds()
    {
        var m = Machine();
        PursueFrom(m, Astern600);
        SustainPursuit(m, Astern600, Chasing);
        Assert.Equal(AiMode.LayOff, m.Mode);

        // The anti-chatter hold: a caught-up gap inside it does not exit yet.
        var astern200 = Home + new Vector3(0f, 0f, 200f);
        Assert.Equal(AiMode.LayOff,
            m.Update(Home, Level, astern200, null, 1f / 60f, Chasing, targetIsHuman: true));

        // Hold out the dwell, then the pursuer catching up returns to pursue.
        for (int i = 0; i < (int)(AiModeMachine.LayOffMinHoldS * 60f) + 5; i++)
            m.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true);
        Assert.Equal(AiMode.LayOff, m.Mode);
        Assert.Equal(AiMode.Pursue,
            m.Update(Home, Level, astern200, null, 1f / 60f, Chasing, targetIsHuman: true));

        // Re-enter, hold out, then the chase ending (velocity away) also returns to pursue.
        SustainPursuit(m, Astern600, Chasing);
        Assert.Equal(AiMode.LayOff, m.Mode);
        for (int i = 0; i < (int)(AiModeMachine.LayOffMinHoldS * 60f) + 5; i++)
            m.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true);
        Assert.Equal(AiMode.Pursue,
            m.Update(Home, Level, Astern600, null, 1f / 60f, new Vector3(0f, 0f, 80f), targetIsHuman: true));
    }

    [Fact]
    public void SwitchingTheAssistOffReleasesARunningLayOff()
    {
        var m = Machine();
        PursueFrom(m, Astern600);
        SustainPursuit(m, Astern600, Chasing);
        Assert.Equal(AiMode.LayOff, m.Mode);
        m.AssistEnabled = false;
        Assert.Equal(AiMode.Pursue,
            m.Update(Home, Level, Astern600, null, 1f / 60f, Chasing, targetIsHuman: true));
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

    // Holds the pursued geometry through the sustain window plus one frame — the
    // entry now needs it CONTINUOUS, never one passing frame.
    private static void SustainPursuit(AiModeMachine m, Vector3 target, Vector3 chase)
    {
        int frames = (int)(AiModeMachine.LayOffSustainS * 60f) + 2;
        for (int i = 0; i < frames; i++)
            m.Update(Home, Level, target, null, 1f / 60f, chase, targetIsHuman: true);
    }

    // A one-step maneuver the executor finishes in a fraction of a second.
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
