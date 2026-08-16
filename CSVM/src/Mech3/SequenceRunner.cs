using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Mech3;

/// <summary>The sequence interpreter's view of its host runtime — the exactly three points at
/// which <see cref="SequenceRunner"/> reaches back into <see cref="AnimRuntime"/>. Kept a
/// 3-member seam so the interpreter is engine-free and headlessly testable: a test supplies a
/// fake host, drives real <see cref="AnimInstance"/>/<see cref="SequenceRunner"/> objects, and
/// asserts on the recorded dispatches. <see cref="AnimRuntime"/> satisfies it by explicit
/// interface implementation, so <c>Dispatch</c>/<c>EvaluateCondition</c> stay off its own surface.
/// </summary>
public interface ISequenceHost
{
    /// <summary>The debugger's fired-mark hook. A get-only nullable delegate, so the runner's
    /// <c>OnEventDispatched?.Invoke(...)</c> null-conditional short-circuits the whole invocation —
    /// including the <see cref="EventDispatch"/> construction — when no debugger is attached. This
    /// is the documented zero-cost contract on the hot dispatch path.</summary>
    Action<EventDispatch>? OnEventDispatched { get; }

    /// <summary>The completion test the event just dispatched installed, or null — read once,
    /// immediately after a <see cref="Dispatch"/> that returned true. Only a
    /// <c>WAIT_FOR_COMPLETION</c> CALL_ANIMATION installs one; it reads true while the callee
    /// the call actually reached is still running. Decode: docs/org/sequences.md.</summary>
    Func<bool>? PendingWait { get; }

    /// <summary>Executes one event; returns its duration via <paramref name="duration"/> (0 for an
    /// instantaneous state change, the run time for a timed motion/SI script). Returns false only
    /// for control-flow events the runner must interpret itself.</summary>
    bool Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration);

    /// <summary>Evaluates one IF/ELSEIF condition; an unparseable or unknown condition returns
    /// false (the safe skip-the-branch direction).</summary>
    bool EvaluateCondition(AnimData? condition, AnimDefinition def, Node3D? anchor);
}

/// <summary>One runtime event dispatch, reported to <see cref="ISequenceHost.OnEventDispatched"/>.
/// Carries what the debugger's timeline needs to stamp a "fired" mark on the right authored block:
/// the running definition and its anchor (the instance identity), the sequence lane, the event's
/// index within that sequence, and its kind and target name. Raised only for real timed
/// dispatches from a sequence runner, never for the instant RESET_STATE posing — so a mark
/// always corresponds to something the clock actually reached.</summary>
public readonly record struct EventDispatch(
    AnimDefinition Def, Node3D? Anchor, string Sequence, int EventIndex,
    string EventKind, string? EventName);

/// <summary>One running definition: its anchor plus a runner per active sequence. The
/// sequences of a definition run CONCURRENTLY — the C1 train drives its four cars from
/// four sibling sequences, each with its own SI script and its own loop — but at most ONE
/// runner per sequence: the original keeps a sequence's execution state inside the definition's
/// own sequence array, so a sequence is a single instance and cannot run two copies of itself.
/// See <see cref="CallSequence"/>.</summary>
public sealed class AnimInstance
{
    public readonly AnimDefinition Def;
    public readonly Node3D? Anchor;
    public readonly List<SequenceRunner> Runners = new();

    public AnimInstance(AnimDefinition def, Node3D? anchor)
    {
        Def = def;
        Anchor = anchor;
    }

    /// <summary>Seconds since this instance started — the original's <c>anim+0xb0</c>, shared by
    /// every sequence of it and gated on by <c>START_TIME ANIMATION</c>. Never rewound; a LOOP
    /// rewinds only the sequence's own timers. Decode: docs/org/sequences.md.</summary>
    public float Clock { get; private set; }

    /// <summary>No runner is still executing. ⚠ NOT on its own the test for retiring an instance —
    /// see <c>AnimRuntime.Retirable</c> and <c>MotionSet.OwesBounce</c>, which additionally hold an
    /// instance open while one of its
    /// motions still owes a BOUNCE_SEQUENCE, since such a launch is the last event of its sequence
    /// and its runner ends the moment the piece leaves the ground.</summary>
    public bool Finished => Runners.Count == 0;

    public void Advance(ISequenceHost rt, float dt)
    {
        // Before the runners, so a sequence clock and the instance clock advance together within
        // a tick — a runner reads Clock back during its own advance to gate origin ANIMATION.
        Clock += dt;
        for (int i = Runners.Count - 1; i >= 0; i--)
        {
            Runners[i].Advance(rt, this, dt);
            if (Runners[i].Done)
                Runners.RemoveAt(i);
        }
    }

    /// <summary>CALL_SEQUENCE: starts this definition's named sequence, but only from the parked
    /// state — a call into an already-running or non-ON_CALL sequence is a silent no-op.
    /// ⚠ Returns whether the definition HAS that sequence, never whether anything started; a
    /// no-op call must still report found. Decode: docs/org/sequences.md.</summary>
    public bool CallSequence(string name)
    {
        var seq = Def.Sequences.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (seq == null)
            return false;
        if (seq.OnCallOnly && !IsRunning(seq))
            AddRunner(seq);
        return true;
    }

    /// <summary>Is a runner for exactly this sequence still executing? Identity is the
    /// <see cref="AnimSequence"/> OBJECT, never its name: the empty name is not unique
    /// (`he_ground_effect` ships two unnamed sequences), so a name-keyed test would collapse them
    /// into one and lose half the burst.</summary>
    public bool IsRunning(AnimSequence seq)
    {
        foreach (var r in Runners)
            if (ReferenceEquals(r.Sequence, seq) && !r.Done)
                return true;
        return false;
    }

    /// <summary>Starts a runner for one sequence — the ONE way a runner joins an instance, so
    /// nothing can put a sequence into flight behind the list's back. Also the bootstrap, the
    /// death slot and the damage-stage host, feeding sequences not in <c>Def.Sequences</c>.
    /// ⚠ A runner added mid-<see cref="Advance"/> fires its first event one tick late — appended
    /// past the descending walk's cursor. Deliberately unfixed; read `BL-135` and
    /// analysis/bl-135-callsequence-lag/FINDINGS.md before changing it.</summary>
    public void AddRunner(AnimSequence seq)
    {
        Runners.Add(new SequenceRunner(seq, Clock));
    }

    /// <summary>STOP_SEQUENCE: halts every active runner named <paramref name="name"/>, including
    /// the caller's own, and does nothing else — no start-if-not-running path exists.
    /// ⚠ Stopping a parked ON_CALL sequence is a DISABLE, un-callable until the definition resets.
    /// ⚠ CSVM does not persist that DISABLE, unlike the original. ⚠ Returns whether the name
    /// RESOLVED, never whether anything was halted. Decode: docs/org/sequences.md.</summary>
    public bool StopSequence(string name)
    {
        bool halted = false;
        foreach (var r in Runners)
        {
            if (!string.Equals(r.SequenceName, name, StringComparison.OrdinalIgnoreCase))
                continue;
            r.Halt();
            halted = true;
        }

        return halted || Def.Sequences.Any(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Executes one sequence's event list on a clock. An event's <c>start</c> gives an origin
/// ("Animation"/"Sequence"/"Event") and a delay; an absent <c>start</c> encodes as
/// "Animation + 0.0" but behaves as "as soon as the previous event finishes" — see
/// <see cref="SetDue"/>. Scheduling and the origin clocks: docs/org/sequences.md; the
/// authored `START_TIME` encoding: docs/formats/anim-definitions.md.
/// </summary>
public sealed class SequenceRunner
{
    /// <summary>One authored ANIMATION FRAME, in seconds — the unit a <c>LOOP</c> count is
    /// denominated in, measured against the original at 60 fps and at 120 fps. ⚠ Deliberately its
    /// own constant, not <see cref="Utils.GameClock.FixedDt"/>, though the two are equal today:
    /// re-stepping the sim for physics reasons must not halve authored animation timers.
    /// Decode: docs/org/sequences.md.</summary>
    public const float AnimFrame = 1f / 60f;

    private readonly AnimSequence _seq;
    // One entry per open IF: has any branch of that chain already run? Our stand-in for the
    // original's arrival-based distinction between a fall-through and a candidate branch — see
    // Scan for the one place the difference could show. Decode: docs/org/sequences.md.
    private readonly List<bool> _branchTaken = new();
    private int _pc;              // next event index
    private float _clock;         // seconds since this sequence started
    // The owning instance's clock, refreshed from AnimInstance.Clock every advance: the origin
    // ANIMATION gate reads it, and it is NOT this runner's clock whenever a CALL_SEQUENCE started
    // the sequence after t=0. Seeded at construction so the opening gate is right on the first tick.
    private float _animClock;
    private float _due;           // when the next event fires
    private bool _done;
    // The original's u16 pass counter (+0x30): counts UP, never reset except by a fresh
    // runner. See the "Loop" case for the termination test this drives.
    private int _loopPasses;
    // The instant the CURRENT event's start offset is measured from: when the previous
    // event fired, plus that event's own run time. Control flow does not advance it.
    private float _base;
    // Did the current loop iteration schedule any time? Decides whether reaching the
    // LOOP starts the next iteration at once or yields to the next frame.
    private bool _iterScheduledTime;
    // Set when a LOOP rolled over an instantaneous iteration: the next pass is gated one
    // AnimFrame out, applied at the foot of the advance loop so the trailing SetDue() cannot
    // overwrite it. Distinct from _iterScheduledTime, which asks whether the DATA scheduled time.
    private bool _frameGatePending;
    // WAIT_FOR_COMPLETION: the callee-still-running test installed by the last dispatched call,
    // polled once per advance until it reads false. Null whenever this runner is not waiting.
    private Func<bool>? _waitingOn;

    public SequenceRunner(AnimSequence seq, float animClock)
    {
        _seq = seq;
        _animClock = animClock;
        // The first event's OWN offset gates it, so the opening gate is not
        // unconditionally zero — a sequence may legitimately start with a delay.
        SetDue();
    }

    public bool Done => _done;

    /// <summary>The authored sequence this runner is executing — the identity
    /// <see cref="AnimInstance.CallSequence"/> matches on, since sequence NAMES are not
    /// unique within a definition.</summary>
    public AnimSequence Sequence => _seq;

    public string SequenceName => _seq.Name;

    /// <summary>Is this runner holding on a WAIT_FOR_COMPLETION call? Observation only — the
    /// tests and the `wait-for-completion` suite read it; nothing in the runner branches on
    /// it.</summary>
    public bool Waiting => _waitingOn != null;

    // Has some branch of the innermost open IF chain already run? A malformed
    // chain (an ELSE with no IF) reads as "not taken" and writes are dropped, so bad
    // data degrades to running the branch instead of faulting.
    private bool Taken
    {
        get => _branchTaken.Count > 0 && _branchTaken[^1];
        set { if (_branchTaken.Count > 0) _branchTaken[^1] = value; }
    }

    /// <summary>Halts this runner: no further events fire. Safe mid-advance — the advance loop
    /// re-tests Done after every dispatch. ⚠ Does not touch resources already launched (motions,
    /// puffers); their lifetimes outlive the sequence that launched them.</summary>
    public void Halt() => _done = true;

    public void Advance(ISequenceHost rt, AnimInstance inst, float dt)
    {
        _clock += dt;
        _animClock = inst.Clock;
        if (!_done && _waitingOn != null)
        {
            if (_waitingOn())
                return;
            // The callee finished. The wait REPLACES the call's duration, so the next event's
            // "Event + t" offset is measured from this instant, not from when the call fired —
            // otherwise the offset is already behind the clock and collapses to zero.
            _waitingOn = null;
            _base = _clock;
            // The hold was real elapsed time, so an enclosing LOOP must not read this iteration
            // as instantaneous and collect the AnimFrame floor meant for untimed poll loops.
            _iterScheduledTime = true;
            SetDue();
        }
        // Guard against a zero-length sequence looping forever within one frame.
        int fired = 0;
        while (!_done && _pc < _seq.Events.Count && _clock >= _due && fired++ < 256)
        {
            var ev = _seq.Events[_pc];
            if (rt.Dispatch(ev, inst.Def, inst.Anchor, instant: false, out float duration))
            {
                // The null-conditional short-circuits the whole invocation, record construction
                // included, when no debugger hook is attached: zero cost on the hot dispatch path.
                rt.OnEventDispatched?.Invoke(new EventDispatch(
                    inst.Def, inst.Anchor, _seq.Name, _pc, ev.Kind, EventDisplayName(ev)));
                // This event has fired. The NEXT event's offset is measured from this
                // moment plus this event's own run time — the offset belongs to the
                // event that CARRIES it, not to its successor. See SetDue().
                _base = _clock + duration;
                if (duration > 0f)
                {
                    _iterScheduledTime = true;
                }
                _pc++;
                SetDue();
                // WAIT_FOR_COMPLETION gates this sequence's NEXT event only, never the runner's
                // lifetime — a runner-lifetime reading wedges thousands of authored LOOP{-1}
                // sequences open forever. Decode: docs/org/sequences.md.
                if (ev.WaitsForCompletion && _pc < _seq.Events.Count
                    && rt.PendingWait is { } wait)
                {
                    _waitingOn = wait;
                    return; // nothing more fires this pass; the poll above resumes us
                }
                continue;
            }
            // Control flow.
            switch (ev.Kind)
            {
                case "Loop":
                    // Counter increments THEN compares, so an authored 0 is infinite as a
                    // consequence, not a case of its own — 26 shipped routes ship Count 0.
                    // ⚠ Do not special-case 0; it must reach the compare below unchanged.
                    int authored = (int)(ev.Data.Num("value") ?? CountOf(ev) ?? -1f);
                    _loopPasses++;
                    if (authored != -1 && _loopPasses == authored)
                    {
                        _done = true;
                        break;
                    }
                    // ⚠ Test whether the iteration scheduled time, never whether the clock reads
                    // zero: the clock is reset here, so a zero test double-runs an instantaneous
                    // poll body. Decode: docs/org/sequences.md.
                    bool instantIteration = !_iterScheduledTime;
                    // ⚠ Carry the overshoot past the gate into the next iteration; never drop it.
                    // Dropping it quantises every iteration to the render rate (instantaneous) or
                    // rounds a timed period up to the next whole step. Decode: docs/org/sequences.md.
                    float carry = _clock - (instantIteration ? AnimFrame : _due);
                    _pc = 0;
                    _clock = carry > 0f ? carry : 0f;
                    _base = 0f;
                    _iterScheduledTime = false;
                    _branchTaken.Clear(); // a new iteration re-tests every condition
                    SetDue();
                    // Applied after the trailing SetDue() at the foot of the loop body, which
                    // would otherwise silently overwrite a _due written here.
                    _frameGatePending = instantIteration;
                    break;
                case "If":
                    _branchTaken.Add(false);
                    goto case "Elseif";
                case "Elseif":
                    if (Taken)
                    {
                        // Falling out of a branch that ran: everything left in the chain
                        // is dead, so jump to the ENDIF (which pops the frame).
                        _pc = SkipToEnd(_pc);
                        break;
                    }
                    if (rt.EvaluateCondition(ev.Data.Obj("condition"), inst.Def, inst.Anchor))
                    {
                        Taken = true;
                        _pc++;      // run the branch body
                    }
                    else
                    {
                        _pc = NextBranch(_pc); // the next ELSEIF/ELSE/ENDIF
                    }
                    break;
                case "Else":
                    if (Taken)
                        _pc = SkipToEnd(_pc);
                    else
                    {
                        Taken = true;
                        _pc++;
                    }
                    break;
                case "Endif":
                    if (_branchTaken.Count > 0)
                        _branchTaken.RemoveAt(_branchTaken.Count - 1);
                    _pc++;
                    break;
                default:
                    _pc++;
                    break;
            }
            // Control flow moved _pc without firing anything, so re-gate on whatever
            // event we landed on. Its own offset applies (LOOP included — the bowl
            // sign's trailing `Loop {Event 1.2}` is its inter-cycle pause).
            SetDue();
            if (_frameGatePending)
            {
                _frameGatePending = false;
                // A floor, not an addition: the authored offset still wins if longer.
                // ⚠ Do not set _iterScheduledTime here; a frame gate is not the body scheduling
                // time, and treating it as such spins the next pass ungated.
                if (_due < AnimFrame)
                {
                    _due = AnimFrame;
                }
            }
        }
        if (_pc >= _seq.Events.Count)
        {
            _done = true;
        }
    }

    private static float? CountOf(AnimEvent ev) => ev.Data.Num("Count");

    // Best-effort target/name an event carries, for the timeline mark's label — the different
    // event kinds spell it under different keys. Nothing load-bearing hangs off it; the event's
    // index within its sequence is what identifies the authored block.
    private static string? EventDisplayName(AnimEvent ev) =>
        ev.Data.Str("name") ?? ev.Data.Str("node") ?? ev.Data.Str("child");

    // Gate the event at _pc on ITS OWN schedule. ⚠ Do not read the offset off the event just
    // fired and apply it to its successor — that shifts every sequence in the install by one
    // slot (the bowl-sign measurement in docs/org/sequences.md is the demonstration).
    // Control-flow events take no time and so do not advance _base, but they ARE still gated.
    private void SetDue()
    {
        if (_pc >= _seq.Events.Count)
        {
            _due = _base;
            return;
        }
        var ev = _seq.Events[_pc];
        _due = ev.StartOffset switch
        {
            // Restated in this runner's own clock (both tick by the same dt) so one comparison
            // gates every origin, and a LOOP that rewinds _clock re-gates through here anyway.
            "Animation" => _clock + (ev.StartTime - _animClock),
            // Origin SEQUENCE is seq+0x24 — absolute against this sequence's own start.
            "Sequence" => ev.StartTime,
            // ⚠ An absent start encodes as Animation + 0.0 but must not be routed through the
            // animation clock: it would gate ~36k unstamped compiled events on an already-running
            // clock. StartOffset is null for them, so they land here; keep it that way.
            _ => _base + ev.StartTime,
        };
        // ⚠ An authored offset counts as scheduled even when the clock has already run past it —
        // testing only `_due > _clock` misreads a short absolute period as instantaneous and
        // collects the AnimFrame floor meant for untimed poll loops. Decode: docs/org/sequences.md.
        if (_due > _clock || ev.StartTime > 0f)
        {
            _iterScheduledTime = true;
        }
    }

    // The next ELSEIF/ELSE/ENDIF — where a FAILED condition continues. Lands ON the event,
    // so the loop re-dispatches it as the next candidate.
    private int NextBranch(int from) => Scan(from, stopAtElse: true);

    // The next ENDIF — where a branch that ran, or one skipped past its whole chain,
    // continues. Lands ON the ENDIF so it pops the frame.
    private int SkipToEnd(int from) => Scan(from, stopAtElse: false);

    // ⚠ Deliberately NOT nesting-aware: the original breaks on the FIRST byte in its stop set,
    // no depth counter. The two stop sets (the `stopAtElse` flag) must stay separate; 48 shipped
    // `gunhit`/`mag_gunhit` sequences nest and observe the difference. Decode: docs/org/sequences.md.
    private int Scan(int from, bool stopAtElse)
    {
        for (int i = from + 1; i < _seq.Events.Count; i++)
        {
            switch (_seq.Events[i].Kind)
            {
                case "Endif":
                    return i;
                case "Else":
                case "Elseif":
                    if (stopAtElse) return i;
                    break;
            }
        }
        return _seq.Events.Count;
    }
}
