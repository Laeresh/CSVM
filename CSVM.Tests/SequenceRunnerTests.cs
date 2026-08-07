using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The headless charter for the sequence interpreter (<c>src/Mech3/SequenceRunner.cs</c>): each of
/// its documented semantics — every one a shipped, measured bug — encoded as a named test against a
/// hand-authored fixture shaped like the case the comment describes. The fixtures carry NO game data
/// (the no-assets rule covers extracted event lists); the shapes come from the comments, the values
/// are invented. Everything runs through the real <see cref="AnimInstance"/> and
/// <see cref="SequenceRunner"/> — never a re-implemented advance loop — so the reverse-iteration
/// removal and the loop/branch state machine are themselves under test. The host is a
/// <see cref="RecordingHost"/> fake; anchors are always null (the interpreter never dereferences
/// them). Behaviour is documented in docs/formats/anim-definitions.md.
/// </summary>
public class SequenceRunnerTests
{
    // ---- fixture builders (hand-authored shapes; invented values) ----

    private static readonly AnimDefinition Dummy = new();

    // 0.25 / 0.5 offsets and dt are exact in binary float, so a fire lands on a definite step.

    // ---- 1. START_TIME gates the carrying event, not its successor (the bowl sign) ----

    [Fact]
    public void StartTimeGatesTheCarryingEventNotItsSuccessor()
    {
        // The bowl sign: strict des_on/des_off SWAP pairs, only the FIRST of each pair stamped. The
        // shipped bug applied a stamp to the NEXT event, splitting every pair — both variants lit at
        // t=0, then nothing for the gap (38% blank frames). Correct: each pair fires together after
        // its own pause; the unstamped partner fires immediately after its stamped predecessor.
        var host = new RecordingHost();
        var inst = Instance(Seq(
            Swap("on1", "Event", 0.5f), Swap("off1"),
            Swap("on2", "Event", 0.5f), Swap("off2"),
            Loop(0)));

        var t = RunSteps(inst, host, 0.25f, 4);

        Assert.Empty(t[0]);                                   // 0.25s: still gated
        Assert.Equal(new[] { "on1", "off1" }, t[1]);          // 0.50s: the pair fires TOGETHER
        Assert.Empty(t[2]);                                   // 0.75s: the inter-pair pause
        Assert.Equal(new[] { "on2", "off2" }, t[3]);          // 1.00s: next pair together
    }

    // ---- 2. an authored Loop Count 0 means INFINITE, not "stop immediately" ----

    [Fact]
    public void AuthoredLoopCountZeroMeansInfinite()
    {
        // Every one of the install's 26 Count-0 loops is a ground-vehicle route the original drives
        // continuously; reading 0 as "stop" made each car drive once and freeze. Count 0 must run
        // forever — the sequence never finishes and keeps re-firing its body.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("car", "Event", 0.5f), Loop(0)));

        RunSteps(inst, host, 0.25f, 40);   // 10 s

        Assert.False(inst.Finished);       // read "stop" and this would be true after one pass
        Assert.True(host.Fired.Count > 10, $"expected many fires, got {host.Fired.Count}");
    }

    // ---- 3. an instant-iteration loop runs one pass per AUTHORED ANIMATION FRAME ----

    [Fact]
    public void InstantIterationLoopFiresExactlyOncePerAnimFrame()
    {
        // The waterfall/poll idiom [instant body, Loop{-1}] keeps an animation alive. Its body takes
        // no time, so the loop is paced to one pass per SequenceRunner.AnimFrame. Driven AT that
        // rate this is the original double-poll guard unchanged: the measured bug tested "clock == 0"
        // instead of "did this iteration schedule time?", so the reset-to-0 clock let the body run a
        // SECOND time before yielding, and every poll loop in the chapter cost double.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("mist"), Loop(-1)));

        var t = RunSteps(inst, host, SequenceRunner.AnimFrame, 8);

        foreach (var step in t)
            Assert.Single(step);           // exactly one poll per step — never two
        Assert.Equal(8, host.Fired.Count);
    }

    [Theory]
    [InlineData(1f / 30f)]     // below the authored rate: catches up within the frame
    [InlineData(1f / 60f)]     // the authored rate itself
    [InlineData(1f / 144f)]    // not a multiple of it — the case that quantised to 48 Hz
    [InlineData(1f / 240f)]
    public void InstantIterationLoopRateIsIndependentOfTheStep(float dt)
    {
        // A LOOP count is a count of authored animation frames, so its RATE must be 60 Hz of sim
        // time whatever the client renders at. Before this was rate-locked the loop ran one pass per
        // rendered frame: 240 Hz ran every authored timer 4x fast, 144 Hz quantised to 48 Hz (3
        // steps per pass), 30 Hz ran at half speed.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("mist"), Loop(-1)));

        int steps = (int)MathF.Round(2f / dt);          // 2 s of sim time at this step
        RunSteps(inst, host, dt, steps);

        // 2 s x 60 Hz, within one pass for the phase of the first fire.
        Assert.InRange(host.Fired.Count, 119, 121);
    }

    [Theory]
    [InlineData(1f / 30f)]
    [InlineData(1f / 60f)]
    [InlineData(1f / 144f)]
    [InlineData(1f / 240f)]
    public void CountedInstantLoopTakesItsAuthoredFramesInSeconds(float dt)
    {
        // The case that named this: `ref_fueltanks`' fire_n_smoke is [PufferState, LOOP 200] and
        // burns ~3 s in the original — 200 frames at 60 Hz. The count must therefore spend
        // 200 x AnimFrame of SIM time before the sequence finishes, at every step size.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("fire"), Loop(200)));

        float elapsed = 0f;
        for (int i = 0; i < 10000 && !inst.Finished; i++)
        {
            inst.Advance(host, dt);
            elapsed += dt;
        }

        // 200 frames of sim time, give or take the step the last pass quantises onto — the
        // residual is bounded by the step size, never by the count, which is the whole property.
        float want = 200f * SequenceRunner.AnimFrame;
        Assert.True(inst.Finished, "a counted loop must terminate");
        Assert.InRange(elapsed, want - 3f * dt, want + 3f * dt);
    }

    [Fact]
    public void AuthoredPeriodShorterThanAnAnimFrameIsNotStretchedToOne()
    {
        // C3/M05's `ww_balmoral1/2/3` are LOOP 1000 with the period on the Loop event itself — an
        // authored 0.01 s, BELOW SequenceRunner.AnimFrame (0.0167). A loop that carries its own
        // period is timed data, not a tick counter, so the animation-frame pacing must not touch
        // it: floored to a frame these three would run 16.7 s instead of their authored 10 s.
        // The mechanism that keeps them out of it is _iterScheduledTime — SetDue() sets it when
        // the Loop's own offset gates arrival at the Loop, so the iteration reads as timed.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("wing"), Loop(1000, "Sequence", 0.01f)));

        // Stepped finer than the authored period, so the period itself is under test rather than
        // the step it quantises onto. The COARSE-step case is its own test below: a timed path
        // that drops its overshoot passes here at 1/600 yet fails at 1/60.
        float dt = 1f / 600f, elapsed = 0f;
        for (int i = 0; i < 20000 && !inst.Finished; i++)
        {
            inst.Advance(host, dt);
            elapsed += dt;
        }

        Assert.True(inst.Finished, "a counted loop must terminate");
        // 1000 x 0.01 s = 10 s, plus the opening pass's own gate. The band is wide enough not to
        // pin that phase and narrow enough to be nowhere near the 16.7 s an AnimFrame floor gives —
        // discriminating between the two readings is the whole point of the case.
        Assert.InRange(elapsed, 9.9f, 10.2f);
    }

    // ---- 3b. an authored PERIOD is seconds at every step, including one coarser than it ----

    [Theory]
    [InlineData(1f / 30f)]     // coarser than either period under test
    [InlineData(1f / 60f)]     // the shipped case: 0.01 s is 0.6 of a step, 0.02 s is 1.2
    [InlineData(1f / 144f)]
    [InlineData(1f / 600f)]
    public void AuthoredPeriodIsHonouredAtStepsCoarserThanItself(float dt)
    {
        // A timed rollover that resets the clock to zero rounds any period that does not land on
        // a whole step UP to the next one — every iteration, forever: `ww_balmoral1/2/3`
        // (LOOP 1000 @ 0.01 s, authored 10 s) then measures 16.7 s at 60 Hz, 12.5 s at 240 Hz and
        // only reaches 10.0 s at 600 Hz — frame-rate dependence the count path must never have.
        // Carrying `_clock - _due` is what makes an authored
        // period mean seconds at any step; the residual is bounded by one step, never by the count.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("wing"), Loop(1000, "Sequence", 0.01f)));

        float elapsed = 0f;
        for (int i = 0; i < 100000 && !inst.Finished; i++)
        {
            inst.Advance(host, dt);
            elapsed += dt;
        }

        Assert.True(inst.Finished, "a counted loop must terminate");
        // 1000 x 0.01 s = 10 s, plus the opening iteration's own gate (one period) and the step
        // the last pass lands on. The band is nowhere near the 16.7 s this measured at 60 Hz
        // before the fix, which is the discrimination the case exists to make.
        Assert.InRange(elapsed, 10f - 3f * dt, 10f + 0.01f + 3f * dt);
    }

    [Theory]
    [InlineData(0.02f)]        // 1.2 steps at 60 Hz — the 126-loop set (patrolboat, ftank_boom, ...)
    [InlineData(1.0f)]         // 60 steps in real arithmetic, 61 in float32 without the carry
    [InlineData(2.5f)]         // 150 steps; shipsink's period
    public void AnInfiniteTimedLoopHoldsItsPeriodOverManyIterations(float period)
    {
        // The long-horizon pin, and the one that covers the 26 ground-vehicle route animations:
        // the carry must not merely fix the first iteration, it must not COMPOUND over hundreds of
        // them. Two failure directions are both caught here — the pre-carry drop lost up to a step
        // per iteration (a 1.0 s route loop cost 61 steps, so a car ran 1.7% slow indefinitely),
        // and a carry applied twice would run fast without bound.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("route"), Loop(-1, "Sequence", period)));

        const float Dt = 1f / 60f;
        const float Seconds = 60f;
        for (int i = 0; i < (int)(Seconds / Dt); i++)
            inst.Advance(host, Dt);

        // 60 s of sim time at one fire per period, within a single iteration for the opening phase.
        int want = (int)(Seconds / period);
        Assert.InRange(host.Fired.Count, want - 1, want + 1);
    }

    // ---- 4. a loop whose body takes time restarts its next iteration immediately ----

    [Fact]
    public void TimedBodyLoopRestartsIterationImmediately()
    {
        // The counterpart to test 3: a loop whose body scheduled time is already waiting on _due, so
        // it must NOT yield — the next iteration starts in the same frame the loop rolls over. With a
        // 1.0 s motion driven at 0.5 s/step, the loop rolls over on the step the clock reaches 1.0 s
        // and the motion re-fires within that same step (2 fires in 3 steps); a spurious yield would
        // defer the re-fire to step 4 (only 1 fire in 3 steps).
        var host = new RecordingHost { Durations = { ["ObjectMotion"] = 1.0f } };
        var inst = Instance(Seq(Timed("swing"), Loop(-1)));

        var t = RunSteps(inst, host, 0.5f, 3);

        Assert.Equal(new[] { "swing" }, t[0]);   // 0.5s: first motion starts, runs to 1.5s
        Assert.Empty(t[1]);                       // 1.0s: still running
        Assert.Equal(new[] { "swing" }, t[2]);   // 1.5s: loop rolls over AND re-fires same step
        Assert.Equal(2, host.Fired.Count);
    }

    // ---- 5. the trailing Loop's own start offset is honoured as an inter-cycle pause ----

    [Fact]
    public void TrailingLoopStartOffsetIsHonouredBetweenCycles()
    {
        // The bowl sign's trailing `Loop {Event 1.2}` is its pause between cycles. Control flow does
        // not fire an event but IS gated, so the Loop's own offset delays the next iteration. The
        // shipped bug discarded it (the Loop branch hard-reset the gate to zero), so the sign cycled
        // with no pause. Here a 1.0 s trailing offset must space the body's fires one full second
        // (two 0.5 s steps) apart.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("blink", "Event", 0f), Loop(0, "Event", 1.0f)));

        var t = RunSteps(inst, host, 0.5f, 6);

        Assert.Equal(new[] { "blink" }, t[0]);   // 0.5s
        Assert.Empty(t[1]);                       // 1.0s: paused by the trailing offset
        Assert.Equal(new[] { "blink" }, t[2]);   // 1.5s
        Assert.Empty(t[3]);                       // 2.0s: paused
        Assert.Equal(new[] { "blink" }, t[4]);   // 2.5s
    }

    // ---- 6. a taken branch falls through to its own ENDIF, depth-aware; a failed one advances ----

    [Fact]
    public void BranchesFallThroughToEndifDepthAwareAndFailedConditionsAdvance()
    {
        // A nested IF inside the outer branch: the fall-through off a taken branch must skip past
        // ELSEIF/ELSE to the OUTER Endif (index 9), NOT the inner one (index 3) already popped —
        // that is the depth-aware Scan. A failed outer condition must instead advance to the next
        // ELSEIF, again skipping the whole inner chain.
        //
        //  0 If(outer) 1 If(inner) 2 SWAP inner_a 3 Endif 4 SWAP outer_a
        //  5 Elseif(elseif) 6 SWAP branch2 7 Else 8 SWAP else_body 9 Endif 10 SWAP after
        var body = new[]
        {
            Branch("If", "outer"), Branch("If", "inner"), Swap("inner_a"), Ctrl("Endif"),
            Swap("outer_a"),
            Branch("Elseif", "elseif"), Swap("branch2"), Ctrl("Else"), Swap("else_body"),
            Ctrl("Endif"), Swap("after"),
        };

        // Outer + inner taken: inner body, outer tail, then fall-through past elseif/else to after.
        var taken = new RecordingHost { Conditions = { ["outer"] = true, ["inner"] = true } };
        RunSteps(Instance(Seq(body)), taken, 1f, 1);
        Assert.Equal(new[] { "inner_a", "outer_a", "after" }, taken.Fired);

        // Outer false: advance past the inner chain to the elseif, which is true.
        var elseif = new RecordingHost { Conditions = { ["outer"] = false, ["elseif"] = true } };
        RunSteps(Instance(Seq(body)), elseif, 1f, 1);
        Assert.Equal(new[] { "branch2", "after" }, elseif.Fired);
    }

    // ---- 7. an ELSE with no IF degrades to running the branch (malformed-chain safety) ----

    [Fact]
    public void ElseWithoutIfRunsTheBranch()
    {
        // A malformed chain (an ELSE with no open IF) reads as "not taken" and its writes are
        // dropped, so bad data degrades to RUNNING the branch rather than faulting — the safe
        // direction (a skipped branch poses objects wrongly).
        var host = new RecordingHost();
        var inst = Instance(Seq(Ctrl("Else"), Swap("body"), Ctrl("Endif"), Swap("after")));

        RunSteps(inst, host, 1f, 1);

        Assert.Equal(new[] { "body", "after" }, host.Fired);
    }

    // ---- 8. "Animation"/"Sequence" offsets are absolute; "Event"/null are relative to _base ----

    [Fact]
    public void AnimationOffsetsAreAbsoluteAndEventOffsetsAreRelative()
    {
        // A 1.0 s motion pushes _base to 1.5 s by the time the second event is gated. An "Animation"
        // offset of 2.0 s is ABSOLUTE — it fires at t=2.0 s regardless of _base; a relative reading
        // would fire it at _base+2.0 = 3.5 s. The third event's "Event" offset is relative: 0.5 s
        // after the absolute event fired (t=2.5 s).
        var host = new RecordingHost { Durations = { ["ObjectMotion"] = 1.0f } };
        var inst = Instance(Seq(
            Timed("motion", "Event", 0f),
            Swap("absolute", "Animation", 2.0f),
            Swap("relative", "Event", 0.5f)));

        var t = RunSteps(inst, host, 0.5f, 6);

        Assert.Equal(new[] { "motion" }, t[0]);     // 0.5s
        Assert.Empty(t[1]);                          // 1.0s
        Assert.Empty(t[2]);                          // 1.5s
        Assert.Equal(new[] { "absolute" }, t[3]);   // 2.0s absolute — relative would be 3.5s
        Assert.Equal(new[] { "relative" }, t[4]);   // 2.5s = absolute's fire + 0.5s
    }

    // ---- 9. the 256-fires-per-frame guard bounds a runaway sequence within one frame ----

    [Fact]
    public void PerFrameFireGuardCapsDispatchesAt256()
    {
        // The while loop guards against a zero-length sequence firing forever within one frame: at
        // most 256 dispatches per Advance, the rest deferred to the next. 260 instantaneous swaps
        // fire 256 on the first frame (sequence not yet finished) and the remaining 4 on the second.
        var host = new RecordingHost();
        var events = new List<AnimEvent>();
        for (int i = 0; i < 260; i++)
            events.Add(Swap("s"));
        var inst = Instance(Seq(events.ToArray()));

        var t = RunSteps(inst, host, 1f, 2);

        Assert.Equal(256, t[0].Count);   // capped this frame
        Assert.Equal(4, t[1].Count);     // the deferred remainder, next frame
        Assert.True(inst.Finished);
    }

    // ---- 10. an AnimInstance runs its sequences concurrently and removes finished runners ----

    [Fact]
    public void InstanceRunsSequencesConcurrentlyAndRemovesFinishedRunners()
    {
        // The C1 train drives its cars from sibling sequences on one instance, each on its own clock.
        // Two sequences with different offsets fire independently; each runner is removed the frame
        // it finishes (the reverse-iteration removal in AnimInstance.Advance), and the instance is
        // Finished only once both are gone.
        var host = new RecordingHost();
        var inst = Instance(
            Seq(Swap("carA", "Event", 0.5f)),
            Seq(Swap("carB", "Event", 1.5f)));

        Assert.Equal(2, inst.Runners.Count);

        inst.Advance(host, 0.5f);                     // 0.5s: seq A fires and finishes
        Assert.Equal(new[] { "carA" }, host.Fired);
        Assert.Single(inst.Runners);                  // A's runner removed, B still live
        Assert.False(inst.Finished);

        inst.Advance(host, 0.5f);                     // 1.0s: nothing due
        Assert.Single(host.Fired);

        inst.Advance(host, 0.5f);                     // 1.5s: seq B fires and finishes
        Assert.Equal(new[] { "carA", "carB" }, host.Fired);
        Assert.Empty(inst.Runners);
        Assert.True(inst.Finished);
    }

    // ---- 11. STOP_SEQUENCE halts the named running sequence; its later events never fire ----

    [Fact]
    public void StopSequenceHaltsTheRunningTargetAndItsLaterEventsNeverFire()
    {
        // The halt idiom (large_30sec_fire, zepskinfire): the target is a genuinely running
        // sibling, and STOP_SEQUENCE must end it — already-fired events stand, later ones never
        // come. Without the halt, the sibling's loop re-asserts its puffer forever.
        var host = new RecordingHost();
        var emitter = Seq("emitter", Swap("puff", "Event", 0.25f), Swap("late", "Event", 1.0f));
        var main = Seq("main", StopSeq("emitter", "Event", 0.5f));
        var inst = Instance(new[] { emitter, main }, emitter, main);
        host.Instance = inst;

        var t = RunSteps(inst, host, 0.25f, 8);

        Assert.Equal(new[] { "puff" }, t[0]);          // 0.25s: the target fired normally
        Assert.DoesNotContain("late", host.Fired);     // due 1.25s, but halted at 0.5s
        Assert.True(inst.Finished);
    }

    // ---- 12. STOP_SEQUENCE on its own sequence breaks out the same frame (test_player) ----

    [Fact]
    public void StopSequenceOnItsOwnSequenceBreaksOutSameFrame()
    {
        // The break idiom (test_player ×33, setprop ×8): a sequence stops ITSELF once its taken
        // branch ran, so the remaining events must not fire — and the exit happens inside the
        // same Advance (the while loop re-tests Done after every dispatch), not via the
        // 256-fire guard.
        var host = new RecordingHost();
        var main = Seq("main", Swap("before"), StopSeq("main"), Swap("after"));
        var inst = Instance(new[] { main }, main);
        host.Instance = inst;

        var t = RunSteps(inst, host, 1f, 1);

        Assert.Equal(new[] { "before", "main" }, t[0]);   // the stop itself is the last dispatch
        Assert.DoesNotContain("after", host.Fired);
        Assert.True(inst.Finished);                        // removed by the sweep, same Advance
    }

    // ---- 13. STOP_SEQUENCE with no running target calls it, exactly like CALL_SEQUENCE ----

    [Fact]
    public void StopSequenceWithNoRunningTargetCallsItLikeCallSequence()
    {
        // The stopper idiom, shaped like the rocket fireball: activate starts a trail and, a
        // beat later, names an ON_CALL stopper nothing else calls. Not running -> STOP_SEQUENCE
        // invokes it, and the stopper's own events dispatch in order on the next frames.
        var host = new RecordingHost();
        var activate = Seq("activate",
            Swap("on"), CallSeq("trail"), StopSeq("stopper", "Event", 0.5f));
        var trail = Seq("trail", Swap("emit"));
        var stopper = Seq("stopper", Swap("off1"), Swap("off2"));
        var inst = Instance(new[] { activate, trail, stopper }, activate);
        host.Instance = inst;

        var t = RunSteps(inst, host, 0.25f, 5);

        Assert.Equal(new[] { "on", "trail" }, t[0]);      // 0.25s: start + the call
        Assert.Equal(new[] { "emit" }, t[1]);             // 0.50s: the called trail runs
        Assert.Equal(new[] { "stopper" }, t[2]);          // 0.75s: the stop finds nothing running
        Assert.Equal(new[] { "off1", "off2" }, t[3]);     //        and calls the stopper instead
        Assert.True(inst.Finished);
    }

    // ---- 14. STOP_SEQUENCE halts every duplicate runner of the name ----

    [Fact]
    public void StopSequenceHaltsEveryDuplicateRunnerOfTheName()
    {
        // CALL_SEQUENCE legitimately starts duplicate concurrent runners of one sequence;
        // "stop that sequence" in the data names the sequence, not one copy — every runner
        // carrying the name halts.
        var host = new RecordingHost();
        var trail = Seq("trail", Swap("emit", "Event", 0.5f));
        var main = Seq("main", StopSeq("trail", "Event", 0.25f));
        var inst = Instance(new[] { trail, main }, trail, trail, main);
        host.Instance = inst;

        RunSteps(inst, host, 0.25f, 4);

        Assert.DoesNotContain("emit", host.Fired);   // both copies halted before their 0.5s fire
        Assert.True(inst.Finished);
    }

    // ---- 15. a STOP_SEQUENCE name miss is a no-op and the sequence continues ----

    [Fact]
    public void StopSequenceNameMissIsANoOpAndTheSequenceContinues()
    {
        // A name matching no runner and no sequence must neither fault nor gate the rest of
        // the sequence — the same degrade-to-continuing direction the malformed-IF rule takes.
        var host = new RecordingHost();
        var main = Seq("main", StopSeq("ghost"), Swap("after"));
        var inst = Instance(new[] { main }, main);
        host.Instance = inst;

        var t = RunSteps(inst, host, 1f, 1);

        Assert.Equal(new[] { "ghost", "after" }, t[0]);
        Assert.True(inst.Finished);
    }

    // ---- WAIT_FOR_COMPLETION: the call gates the NEXT event, on the callee, not on a clock ----

    [Fact]
    public void WaitForCompletionHoldsTheNextEventUntilTheCalleeFinishes()
    {
        // The authored case (player_crash_water/destroy_crash): a flagged CALL_ANIMATION of
        // plane_big_splash, then large_steam_spray. If every call returned immediately, both would
        // retarget on the SAME tick; the splash's own choreography runs 3.0 s.
        var host = new RecordingHost();
        host.Running["plane_big_splash"] = true;
        var inst = Instance(Seq(Call("plane_big_splash", wait: true), Call("large_steam_spray")));

        var t = RunSteps(inst, host, 0.5f, 3);
        Assert.Equal(new[] { "plane_big_splash" }, t[0]);   // the call fires...
        Assert.Empty(t[1]);                                 // ...and the spray is held
        Assert.Empty(t[2]);
        Assert.False(inst.Finished);

        host.Running["plane_big_splash"] = false;           // the callee's 3.0 s choreography ends
        var after = RunSteps(inst, host, 0.5f, 1);
        Assert.Equal(new[] { "large_steam_spray" }, after[0]);
        Assert.True(inst.Finished);
    }

    [Fact]
    public void AnUnflaggedCallDoesNotHold()
    {
        // METHOD-9/METHOD-10: the same fixture with the flag cleared must NOT hold, or the test
        // above would pass on a runner that blocks every call. `0` and `null` are different
        // authored states (3,639 vs 53,019) and this is the `null` one.
        var host = new RecordingHost();
        host.Running["plane_big_splash"] = true;
        var inst = Instance(Seq(Call("plane_big_splash"), Call("large_steam_spray")));

        var t = RunSteps(inst, host, 0.5f, 1);

        Assert.Equal(new[] { "plane_big_splash", "large_steam_spray" }, t[0]);
    }

    [Fact]
    public void AFlaggedCallOnANonRunningCalleeDoesNotHold()
    {
        // A callee whose whole choreography fires at t=0 never becomes a live instance, so the
        // host installs no test and the caller must advance in the same pass — 16 of the census's
        // effective holds are that shape. A runner that held on a missing test would freeze them.
        var host = new RecordingHost();
        var inst = Instance(Seq(Call("instant_effect", wait: true), Call("after")));

        var t = RunSteps(inst, host, 0.5f, 1);

        Assert.Equal(new[] { "instant_effect", "after" }, t[0]);
    }

    [Fact]
    public void AFlaggedCallAsTheLastEventDoesNotHoldItsRunnerOpen()
    {
        // THE scope rule, and it is the data's: 2,770 of the 2,999 flagged calls the runtime can
        // reach are the last event of their block, and every one of those names a callee that
        // NEVER terminates (the sputter_* / gen_drop_ladder LOOP{-1} idiom). Read as a lifetime
        // hold, all 2,770 would wedge their sequence open for the session; read as a gate on the
        // next event, there is nothing behind the call to gate and the runner retires as before.
        var host = new RecordingHost();
        host.Running["sputter_fire"] = true;              // and it never stops
        var inst = Instance(Seq(Swap("burn"), Call("sputter_fire", wait: true)));

        RunSteps(inst, host, 0.5f, 2);

        Assert.True(inst.Finished);
        Assert.Equal(new[] { "burn", "sputter_fire" }, host.Fired);
    }

    [Fact]
    public void TheEventAfterAWaitIsScheduledFromTheCalleesEndNotFromTheCall()
    {
        // The wait REPLACES the call's duration, so a trailing "Event + t" offset is measured from
        // completion. Computed from the call instead, the offset is already behind the clock by
        // release time and collapses to zero — the held event would fire in the same pass the
        // callee ended, losing its authored pause.
        var host = new RecordingHost();
        host.Running["callee"] = true;
        var inst = Instance(Seq(Call("callee", wait: true), Swap("after", "Event", 0.5f)));

        RunSteps(inst, host, 0.25f, 4);          // 1.0 s of holding
        host.Running["callee"] = false;
        var t = RunSteps(inst, host, 0.25f, 4);

        // t[0] is the pass that OBSERVES the callee gone and re-bases; the offset runs from there.
        // (In the game that pass is the same tick the callee ended: AnimRuntime.Advance walks its
        // instances backwards, and a callee started by this caller sits later in the list, so it
        // is retired before the caller polls.)
        Assert.Empty(t[0]);
        Assert.Empty(t[1]);                       // release + 0.25 s: still inside the 0.5 s offset
        Assert.Equal(new[] { "after" }, t[2]);    // release + 0.50 s: the authored pause, honoured
        Assert.Empty(t[3]);
    }

    [Fact]
    public void AWaitInsideAPollLoopStopsTheLoopSpinningWhileItHolds()
    {
        // The data's poll idiom is an INSTANTANEOUS `If … CallAnimation; Endif; Loop{-1}` body,
        // paced to one pass per AnimFrame — so a hold inside one has to stop the whole loop, not
        // just the next event. Unheld, five seconds of this fixture re-fire the body ~300 times.
        var host = new RecordingHost();
        host.Running["callee"] = true;
        var inst = Instance(Seq(Call("callee", wait: true), Swap("body"), Loop(0)));

        RunSteps(inst, host, 0.5f, 10);                      // 5 s of holding

        Assert.Equal(new[] { "callee" }, host.Fired);        // one call, and nothing behind it
        Assert.False(inst.Finished);

        host.Running["callee"] = false;
        RunSteps(inst, host, 0.5f, 1);

        // Released, the iteration completes and the loop wraps in that same pass — the hold was
        // real elapsed time, so the iteration is not ALSO charged the AnimFrame floor that paces
        // untimed poll loops (SequenceRunner sets _iterScheduledTime on release for this).
        Assert.Equal(new[] { "callee", "body", "callee" }, host.Fired.GetRange(0, 3));
    }

    private static AnimInstance Instance(params AnimSequence[] seqs)
    {
        var inst = new AnimInstance(Dummy, null);
        foreach (var s in seqs)
            inst.Runners.Add(new SequenceRunner(s));
        return inst;
    }

    /// <summary>An instance over a definition that KNOWS the given sequences (so CALL_SEQUENCE /
    /// STOP_SEQUENCE can look them up by name), with runners started only for
    /// <paramref name="run"/> — the rest sit ON_CALL.</summary>
    private static AnimInstance Instance(AnimSequence[] defined, params AnimSequence[] run)
    {
        var def = new AnimDefinition();
        def.Sequences.AddRange(defined);
        var inst = new AnimInstance(def, null);
        foreach (var s in run)
            inst.Runners.Add(new SequenceRunner(s));
        return inst;
    }

    private static AnimSequence Seq(params AnimEvent[] events) => Seq("test", events);

    private static AnimSequence Seq(string name, params AnimEvent[] events)
    {
        var s = new AnimSequence { Name = name };
        s.Events.AddRange(events);
        return s;
    }

    /// <summary>An instantaneous OBJECT_ACTIVE_STATE swap named <paramref name="name"/> (the SWAP the
    /// bowl sign flickers with).</summary>
    private static AnimEvent Swap(string name, string? offset = null, float time = 0f) =>
        new() { Kind = "ObjectActiveState", StartOffset = offset, StartTime = time,
                Data = new AnimData(new Dictionary<string, object?> { ["name"] = name }) };

    /// <summary>A timed motion whose run time the host reports (its Kind keys
    /// <see cref="RecordingHost.Durations"/>).</summary>
    private static AnimEvent Timed(string name, string? offset = null, float time = 0f) =>
        new() { Kind = "ObjectMotion", StartOffset = offset, StartTime = time,
                Data = new AnimData(new Dictionary<string, object?> { ["name"] = name }) };

    private static AnimEvent CallSeq(string name, string? offset = null, float time = 0f) =>
        new() { Kind = "CallSequence", StartOffset = offset, StartTime = time,
                Data = new AnimData(new Dictionary<string, object?> { ["name"] = name }) };

    private static AnimEvent StopSeq(string name, string? offset = null, float time = 0f) =>
        new() { Kind = "StopSequence", StartOffset = offset, StartTime = time,
                Data = new AnimData(new Dictionary<string, object?> { ["name"] = name }) };

    private static AnimEvent Loop(int count, string? offset = null, float time = 0f) =>
        new() { Kind = "Loop", StartOffset = offset, StartTime = time,
                Data = new AnimData(new Dictionary<string, object?> { ["Count"] = count }) };

    private static AnimEvent Call(string callee, bool wait = false, string? offset = null, float time = 0f) =>
        new() { Kind = "CallAnimation", StartOffset = offset, StartTime = time,
                WaitsForCompletion = wait,
                Data = new AnimData(new Dictionary<string, object?> { ["name"] = callee }) };

    private static AnimEvent Branch(string kind, string tag) =>
        new() { Kind = kind,
                Data = new AnimData(new Dictionary<string, object?>
                    { ["condition"] = new Dictionary<string, object?> { ["tag"] = tag } }) };

    private static AnimEvent Ctrl(string kind) => new() { Kind = kind };

    /// <summary>Drives the instance in fixed <paramref name="dt"/> steps and returns, per step, the
    /// names dispatched during that step — so a test can assert both order and which step each fire
    /// landed on (the timing evidence).</summary>
    private static List<List<string>> RunSteps(AnimInstance inst, RecordingHost host, float dt, int steps)
    {
        var timeline = new List<List<string>>();
        for (int i = 0; i < steps; i++)
        {
            int before = host.Fired.Count;
            inst.Advance(host, dt);
            timeline.Add(host.Fired.GetRange(before, host.Fired.Count - before));
        }
        return timeline;
    }

    // ---- the fake host: the interpreter's 3-point view, scripted and recording ----

    /// <summary>An <see cref="ISequenceHost"/> that records every real dispatch, returns a scripted
    /// duration per event kind, and answers conditions from a tag→verdict table. Control-flow kinds
    /// return <c>false</c> from <see cref="Dispatch"/> — returning true would silently bypass the
    /// LOOP/IF branch logic and every test would pass while testing nothing.</summary>
    private sealed class RecordingHost : ISequenceHost
    {
        public readonly List<string> Fired = new();
        public readonly Dictionary<string, float> Durations = new(StringComparer.Ordinal);
        public readonly Dictionary<string, bool> Conditions = new(StringComparer.Ordinal);
        public readonly List<EventDispatch> Dispatched = new();

        /// <summary>Callee name → is that callee still running. The real host closes over the
        /// (def, anchor) instances a call reached; a test needs only the answer, so this stands
        /// in for the whole of that — set an entry true and the wait holds, false and it releases.
        /// A callee with no entry installs no wait at all, which is the real host's
        /// "nothing live to hold on" case.</summary>
        public readonly Dictionary<string, bool> Running = new(StringComparer.Ordinal);

        /// <summary>The instance CALL_SEQUENCE/STOP_SEQUENCE act on — the same thin routing the
        /// real runtime's dispatch does; the composition under test is <see cref="AnimInstance"/>'s.
        /// </summary>
        public AnimInstance? Instance;

        private static readonly HashSet<string> ControlFlow =
            new(StringComparer.Ordinal) { "Loop", "If", "Elseif", "Else", "Endif" };

        public Action<EventDispatch>? OnEventDispatched { get; set; }

        public Func<bool>? PendingWait { get; private set; }

        public bool Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration)
        {
            duration = 0f;
            PendingWait = null;
            if (ControlFlow.Contains(ev.Kind))
                return false;
            if (ev.WaitsForCompletion && ev.Data.Str("name") is { } callee
                && Running.TryGetValue(callee, out bool live) && live)
            {
                PendingWait = () => Running.TryGetValue(callee, out bool still) && still;
            }
            if (ev.Kind == "CallSequence" && ev.Data.Str("name") is { } callName)
                Instance?.CallSequence(callName);
            else if (ev.Kind == "StopSequence" && ev.Data.Str("name") is { } stopName)
                Instance?.StopSequence(stopName);
            Durations.TryGetValue(ev.Kind, out duration);
            Fired.Add(ev.Data.Str("name") ?? ev.Kind);
            return true;
        }

        public bool EvaluateCondition(AnimData? condition, AnimDefinition def, Node3D? anchor)
        {
            var tag = condition?.Str("tag");
            return tag != null && Conditions.TryGetValue(tag, out var v) && v;
        }
    }
}
