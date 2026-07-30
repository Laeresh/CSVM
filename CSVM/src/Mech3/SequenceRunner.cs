using System;
using System.Collections.Generic;
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
    /// <summary>Executes one event; returns its duration via <paramref name="duration"/> (0 for an
    /// instantaneous state change, the run time for a timed motion/SI script). Returns false only
    /// for control-flow events the runner must interpret itself.</summary>
    bool Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration);

    /// <summary>Evaluates one IF/ELSEIF condition; an unparseable or unknown condition returns
    /// false (the safe skip-the-branch direction).</summary>
    bool EvaluateCondition(AnimData? condition, AnimDefinition def, Node3D? anchor);

    /// <summary>The debugger's fired-mark hook. A get-only nullable delegate, so the runner's
    /// <c>OnEventDispatched?.Invoke(...)</c> null-conditional short-circuits the whole invocation —
    /// including the <see cref="EventDispatch"/> construction — when no debugger is attached. This
    /// is the documented zero-cost contract on the hot dispatch path.</summary>
    Action<EventDispatch>? OnEventDispatched { get; }
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
/// four sibling sequences, each with its own SI script and its own loop.</summary>
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

    public bool Finished => Runners.Count == 0;

    public void Advance(ISequenceHost rt, float dt)
    {
        for (int i = Runners.Count - 1; i >= 0; i--)
        {
            Runners[i].Advance(rt, this, dt);
            if (Runners[i].Done)
                Runners.RemoveAt(i);
        }
    }
}

/// <summary>
/// Executes one sequence's event list on a clock.
///
/// Scheduling (inferred from the data, recorded in docs/formats/anim-definitions.md):
/// an event's <c>start</c> gives an origin and a delay — "Animation" from the animation
/// start, "Sequence" from this sequence's start, "Event" from the previous event's
/// COMPLETION. An absent <c>start</c> is "Event + 0", i.e. as soon as the previous
/// event finishes. That reading is what makes the C1 train work: its sequences are
/// [ObjectMotionSiScript, Loop{-1}] with no start offsets, and only "after the previous
/// event completes" turns that into the surveyed ~327 s track loop rather than a
/// zero-length infinite loop.
/// </summary>
public sealed class SequenceRunner
{
    private readonly AnimSequence _seq;
    private int _pc;              // next event index
    private float _clock;         // seconds since this sequence started
    private float _due;           // when the next event fires
    private bool _done;
    private int _loopsLeft = -2;  // -2 = no loop seen yet
    // The instant the CURRENT event's start offset is measured from: when the previous
    // event fired, plus that event's own run time. Control flow does not advance it.
    private float _base;
    // Did the current loop iteration schedule any time? Decides whether reaching the
    // LOOP starts the next iteration at once or yields to the next frame.
    private bool _iterScheduledTime;
    // One entry per open IF: has any branch of that chain already run? An ELSEIF/ELSE
    // reached with the flag set is the *fall-through* off the end of a taken branch and
    // must skip to the ENDIF; reached with it clear, it is the next candidate to test.
    private readonly List<bool> _branchTaken = new();

    public SequenceRunner(AnimSequence seq)
    {
        _seq = seq;
        // The first event's OWN offset gates it, so the opening gate is not
        // unconditionally zero — a sequence may legitimately start with a delay.
        SetDue();
    }

    public bool Done => _done;

    public void Advance(ISequenceHost rt, AnimInstance inst, float dt)
    {
        _clock += dt;
        // Guard against a zero-length sequence looping forever within one frame.
        int fired = 0;
        while (!_done && _pc < _seq.Events.Count && _clock >= _due && fired++ < 256)
        {
            var ev = _seq.Events[_pc];
            if (rt.Dispatch(ev, inst.Def, inst.Anchor, instant: false, out float duration))
            {
                // The debugger timeline's "fired" mark: this event, in this sequence lane, at
                // the playhead time the runner reached it. The null-conditional short-circuits
                // the whole invocation — including the record construction — when no hook is
                // attached, so this is zero cost in the game (it is on the hot dispatch path).
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
                continue;
            }
            // Control flow.
            switch (ev.Kind)
            {
                case "Loop":
                    if (_loopsLeft == -2)
                    {
                        int authored = (int)(ev.Data.Num("value") ?? CountOf(ev) ?? -1f);
                        // An AUTHORED count of 0 means INFINITE, not "stop immediately".
                        // Surveyed across the whole install: 26 Loop events
                        // in 25 defs ship Count 0, and every one of them is a ground-vehicle
                        // route (C1's police/mafia/black_car/truck traffic, C2's and C3/M02's
                        // studebakers) whose Loop is the LAST event of its sequence — the
                        // original drives these continuously. Nothing that must terminate
                        // uses it: no door, gate, one-shot, bomb or explosion def, and the
                        // reader/zrdr scope has 703 Loop events with zero Count 0. Reading it
                        // as "stop" made each car drive its route once and freeze.
                        // Normalise here rather than at the test below, so the test keeps
                        // meaning "a finite loop has run out" — that is the only way a
                        // positive count can ever terminate.
                        _loopsLeft = authored == 0 ? -1 : authored;
                    }
                    if (_loopsLeft == 0)
                    {
                        _done = true;
                        break;
                    }
                    if (_loopsLeft > 0)
                        _loopsLeft--;
                    // A loop over purely instantaneous events is the data's "keep this
                    // animation alive" idiom (C1's waterfall is [PufferState ×3, Loop{-1}],
                    // whose emitters run on their own TIME_INTERVAL; the poll idiom
                    // `If … CallAnimation; Endif; Loop{-1}` is the other shape). Left
                    // unchecked it spins as fast as the per-frame guard allows; yield to
                    // the next frame instead, so such a loop polls exactly once a frame.
                    // Loops whose body takes time are unaffected — they are already
                    // waiting on _due, and must start their next iteration immediately.
                    //
                    // The test is "did this iteration schedule any time?", NOT "is the
                    // clock zero": the clock is reset to 0 here, so on the NEXT frame it
                    // reads dt at this point and an instantaneous body ran a second time
                    // before yielding — every poll loop in the chapter costing double.
                    bool instantIteration = !_iterScheduledTime;
                    _pc = 0;
                    _clock = 0f;
                    _base = 0f;
                    _iterScheduledTime = false;
                    _branchTaken.Clear(); // a new iteration re-tests every condition
                    SetDue();
                    if (instantIteration)
                    {
                        return;
                    }
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
            // sign's trailing `Loop {Event 1.2}` is its inter-cycle pause, and that
            // offset used to be discarded).
            SetDue();
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

    /// <summary>
    /// Gate the event at <see cref="_pc"/> on ITS OWN schedule.
    ///
    /// An event's START_TIME says when *that* event fires — see
    /// <see cref="AnimEvent.StartOffset"/>: "Event" = since the previous event fired,
    /// null = immediately after it. This used to be computed from the event just
    /// FIRED and applied to its successor, which shifted **every sequence in the
    /// install** by one slot: a timestamped event fired one slot early and its
    /// unstamped partner one slot late.
    ///
    /// C1's `bowl` sign is the clean demonstration. Its compiled
    /// sequence is nine strict `des_on`/`des_off` SWAP pairs plus an infinite Loop,
    /// and only the FIRST of each pair carries a timestamp — so the shift split every
    /// pair, leaving both variants lit at t=0 and then **nothing at all** for each
    /// gap. Measured face-on at the sign: 38.0% of frames completely blank before,
    /// 0% after. The user's report was "the bowl sign flashes in the original, but
    /// ours disables and re-enables it instead".
    ///
    /// Control-flow events (LOOP/IF/ELSEIF/…) do not advance <see cref="_base"/> —
    /// they take no time — but they ARE gated, which is what gives the sign's trailing
    /// `Loop {Event 1.2}` its inter-cycle pause. That offset was previously discarded
    /// outright, since the Loop branch hard-reset the gate to zero.
    /// </summary>
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
            // "Animation"/"Sequence" are absolute against their origin; this runner's
            // clock is the sequence clock, and an instance starts all its sequences
            // together, so the two coincide for every case in the shipped data.
            "Animation" or "Sequence" => ev.StartTime,
            _ => _base + ev.StartTime,
        };
        if (_due > _clock)
        {
            _iterScheduledTime = true;
        }
    }

    /// <summary>Has some branch of the innermost open IF chain already run? A malformed
    /// chain (an ELSE with no IF) reads as "not taken" and writes are dropped, so bad
    /// data degrades to running the branch instead of faulting.</summary>
    private bool Taken
    {
        get => _branchTaken.Count > 0 && _branchTaken[^1];
        set { if (_branchTaken.Count > 0) _branchTaken[^1] = value; }
    }

    // The next ELSEIF/ELSE/ENDIF of this chain (nesting-aware) — where a FAILED condition
    // continues. Lands ON the event, so the loop re-dispatches it as the next candidate.
    private int NextBranch(int from) => Scan(from, stopAtElse: true);

    // The chain's own ENDIF — where a branch that ran, or one skipped past its whole
    // chain, continues. Lands ON the ENDIF so it pops the frame.
    private int SkipToEnd(int from) => Scan(from, stopAtElse: false);

    private int Scan(int from, bool stopAtElse)
    {
        int depth = 0;
        for (int i = from + 1; i < _seq.Events.Count; i++)
        {
            switch (_seq.Events[i].Kind)
            {
                case "If": depth++; break;
                case "Endif":
                    if (depth == 0) return i;
                    depth--;
                    break;
                case "Else":
                case "Elseif":
                    if (depth == 0 && stopAtElse) return i;
                    break;
            }
        }
        return _seq.Events.Count;
    }
}
