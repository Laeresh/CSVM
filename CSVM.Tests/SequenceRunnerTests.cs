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
    // ---- the fake host: the interpreter's 3-point view, scripted and recording ----

    /// <summary>An <see cref="ISequenceHost"/> that records every real dispatch, returns a scripted
    /// duration per event kind, and answers conditions from a tag→verdict table. Control-flow kinds
    /// return <c>false</c> from <see cref="Dispatch"/> — returning true would silently bypass the
    /// LOOP/IF branch logic and every test would pass while testing nothing.</summary>
    private sealed class RecordingHost : ISequenceHost
    {
        private static readonly HashSet<string> ControlFlow =
            new(StringComparer.Ordinal) { "Loop", "If", "Elseif", "Else", "Endif" };

        public readonly List<string> Fired = new();
        public readonly Dictionary<string, float> Durations = new(StringComparer.Ordinal);
        public readonly Dictionary<string, bool> Conditions = new(StringComparer.Ordinal);
        public readonly List<EventDispatch> Dispatched = new();

        public bool Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration)
        {
            duration = 0f;
            if (ControlFlow.Contains(ev.Kind))
                return false;
            Durations.TryGetValue(ev.Kind, out duration);
            Fired.Add(ev.Data.Str("name") ?? ev.Kind);
            return true;
        }

        public bool EvaluateCondition(AnimData? condition, AnimDefinition def, Node3D? anchor)
        {
            var tag = condition?.Str("tag");
            return tag != null && Conditions.TryGetValue(tag, out var v) && v;
        }

        public Action<EventDispatch>? OnEventDispatched { get; set; }
    }

    // ---- fixture builders (hand-authored shapes; invented values) ----

    private static readonly AnimDefinition Dummy = new();

    private static AnimInstance Instance(params AnimSequence[] seqs)
    {
        var inst = new AnimInstance(Dummy, null);
        foreach (var s in seqs)
            inst.Runners.Add(new SequenceRunner(s));
        return inst;
    }

    private static AnimSequence Seq(params AnimEvent[] events)
    {
        var s = new AnimSequence { Name = "test" };
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

    private static AnimEvent Loop(int count, string? offset = null, float time = 0f) =>
        new() { Kind = "Loop", StartOffset = offset, StartTime = time,
                Data = new AnimData(new Dictionary<string, object?> { ["Count"] = count }) };

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

    // ---- 3. an instant-iteration loop yields exactly once per frame (the double-poll waterfall) ----

    [Fact]
    public void InstantIterationLoopFiresExactlyOncePerFrame()
    {
        // The waterfall/poll idiom [instant body, Loop{-1}] keeps an animation alive. Its body takes
        // no time, so the loop must yield to the next frame — polling once a frame. The measured bug
        // tested "clock == 0" instead of "did this iteration schedule time?", so the reset-to-0 clock
        // let the body run a SECOND time before yielding: every poll loop cost double.
        var host = new RecordingHost();
        var inst = Instance(Seq(Swap("mist"), Loop(-1)));

        var t = RunSteps(inst, host, 0.5f, 8);

        foreach (var step in t)
            Assert.Single(step);           // exactly one poll per frame — never two
        Assert.Equal(8, host.Fired.Count);
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
}
