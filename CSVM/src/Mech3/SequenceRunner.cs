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

    /// <summary>CALL_SEQUENCE: adds a runner for this definition's named sequence. Duplicates
    /// are legitimate — a second call runs a second concurrent copy. Returns whether the
    /// definition has that sequence.</summary>
    public bool CallSequence(string name)
    {
        var seq = Def.Sequences.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (seq == null)
            return false;
        Runners.Add(new SequenceRunner(seq));
        return true;
    }

    /// <summary>STOP_SEQUENCE: halts every active runner named <paramref name="name"/> —
    /// including the caller's own runner (the data's break-out-of-my-own-IF-chain idiom). If
    /// none is running, starts the sequence exactly like CALL_SEQUENCE instead (the stopper
    /// idiom: reaching an ON_CALL teardown sequence nothing else calls). Semantics decoded in
    /// docs/formats/anim-definitions.md. Returns false only when the name matched no runner
    /// and no sequence.</summary>
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
        return halted || CallSequence(name);
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
    /// <summary>One authored ANIMATION FRAME, in seconds — the unit a <c>LOOP</c> count is
    /// denominated in. A <c>LOOP n</c> over an instantaneous body can only advance one pass per
    /// engine update, so it is a timer of n updates; 1/60 s is where we PUT that update, which is
    /// a decision and not a decode of the original. The evidence is `ref_fueltanks`'
    /// <c>fire_n_smoke</c> (<c>LOOP 200</c>) observed burning ~3 s in the original — but on modern
    /// hardware, where it runs visibly fast or slow between sessions, so it may only be recording
    /// that machine's 60 Hz vsync cap. It cannot tell a fixed ~60 Hz sequence tick from per-frame
    /// ticking (under which the original had no single correct duration at all). 1/60 is right
    /// either way as the rate the content was authored against — see
    /// docs/formats/anim-definitions.md for the falsification that would settle it.
    ///
    /// <para>⚠ Deliberately its OWN constant, not <see cref="Utils.GameClock.FixedDt"/>, though the
    /// two are equal today. That equality is what keeps every `--det` capture byte-identical
    /// (one pass per fixed step, exactly as before this was rate-locked) — but "the rate the
    /// original's artists counted frames at" and "the rate we step the simulation at" are
    /// independent facts. Re-stepping the sim at 1/120 for physics reasons must NOT halve every
    /// authored animation timer.</para></summary>
    public const float AnimFrame = 1f / 60f;

    private readonly AnimSequence _seq;
    // One entry per open IF: has any branch of that chain already run? An ELSEIF/ELSE
    // reached with the flag set is the *fall-through* off the end of a taken branch and
    // must skip to the ENDIF; reached with it clear, it is the next candidate to test.
    private readonly List<bool> _branchTaken = new();
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
    // Set when a LOOP rolled over an instantaneous iteration: the next pass is gated one
    // AnimFrame out, applied at the foot of the advance loop so the trailing SetDue() cannot
    // overwrite it. Distinct from _iterScheduledTime, which asks whether the DATA scheduled time.
    private bool _frameGatePending;

    public SequenceRunner(AnimSequence seq)
    {
        _seq = seq;
        // The first event's OWN offset gates it, so the opening gate is not
        // unconditionally zero — a sequence may legitimately start with a delay.
        SetDue();
    }

    public bool Done => _done;

    public string SequenceName => _seq.Name;

    /// <summary>Has some branch of the innermost open IF chain already run? A malformed
    /// chain (an ELSE with no IF) reads as "not taken" and writes are dropped, so bad
    /// data degrades to running the branch instead of faulting.</summary>
    private bool Taken
    {
        get => _branchTaken.Count > 0 && _branchTaken[^1];
        set { if (_branchTaken.Count > 0) _branchTaken[^1] = value; }
    }

    /// <summary>Halts this runner: no further events fire. Safe mid-advance — the advance loop
    /// re-tests Done after every dispatch, so a self-halt exits before the next event, and
    /// <see cref="AnimInstance.Advance"/>'s sweep removes the runner. Resources the sequence
    /// already launched (motions, puffers) are untouched: their lifetimes are authored
    /// independently and outlive the sequence that launched them.</summary>
    public void Halt() => _done = true;

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
                    // unchecked it spins as fast as the per-frame guard allows; it is paced
                    // to one pass per AnimFrame of SIM time instead — the rate the counts
                    // were authored against. Loops whose body takes time are unaffected —
                    // they are already waiting on _due, and must start their next iteration
                    // immediately.
                    //
                    // The test is "did this iteration schedule any time?", NOT "is the
                    // clock zero": the clock is reset here, so a clock-zero test would read
                    // dt at this point on the next pass and let an instantaneous body run a
                    // second time before yielding — every poll loop in the chapter costing
                    // double. That measured bug is why the flag exists; keep it.
                    bool instantIteration = !_iterScheduledTime;
                    // An instantaneous iteration costs exactly one authored ANIMATION FRAME
                    // (AnimFrame), not "one rendered frame" — that is what makes a LOOP count a
                    // duration instead of a function of the client's frame rate. Carry the sim
                    // time that overshot the frame we just spent into the next iteration, or the
                    // rate quantises to the render rate: a 144 Hz client needs 3 frames to reach
                    // 1/60 and would run every authored timer at 48 Hz, a 30 Hz one at half speed.
                    //
                    // A TIMED iteration carries the same way, against what it actually waited on:
                    // _due, which at this point is still the Loop event's own gate (the while
                    // condition above passed on it, and nothing between here and there rewrites
                    // it). An authored period is SECONDS, so dropping the overshoot rounded every
                    // iteration up to the next whole step — the same frame-rate dependence, one
                    // level down. Measured over the install's 599 timed loops (`BL-237`): a 0.02 s
                    // period cost 2 steps instead of 1.2 at 60 Hz, so the 126 loops carrying it
                    // (`patrolboat`, `ptboat*`, `ftank_boom*`, `m_build0*`, `pass_plane0*`,
                    // `sub_destruction`, `balloont_die*`, `refuel*`) ran at 60% speed; C3/M05's
                    // `ww_balmoral1/2/3` (LOOP 1000 @ 0.01 s, authored 10 s) took 16.7 s at 60 Hz.
                    // Even the periods that divide 1/60 exactly in real arithmetic paid one extra
                    // step per iteration, because sixty float32 additions of 1/60 land just under
                    // 1.0 and the gate is `>=`: 61 steps per second, forever, on every route loop.
                    float carry = _clock - (instantIteration ? AnimFrame : _due);
                    _pc = 0;
                    _clock = carry > 0f ? carry : 0f;
                    _base = 0f;
                    _iterScheduledTime = false;
                    _branchTaken.Clear(); // a new iteration re-tests every condition
                    SetDue();
                    // The gate itself is applied after the trailing SetDue() at the foot of this
                    // loop body — that call re-gates on whatever event control flow landed on and
                    // would silently overwrite a _due written here. The pre-rate-lock code
                    // sidestepped it with an early `return`, and THAT is what made the pass rate
                    // the render rate; dropping the return is the whole change.
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
            // sign's trailing `Loop {Event 1.2}` is its inter-cycle pause, and that
            // offset used to be discarded).
            SetDue();
            if (_frameGatePending)
            {
                _frameGatePending = false;
                // An instantaneous loop pass costs one authored animation frame. The authored
                // offset still wins whenever it is the longer wait (the bowl sign's 1.2 s pause):
                // a body that asks for real time has already paid for its frame, so this is a
                // floor, not an addition. _iterScheduledTime is deliberately NOT set — a frame
                // gate is not the body scheduling time, and treating it as such would make the
                // NEXT pass read as timed and spin ungated.
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
        // "Did the DATA schedule time?" — an authored offset counts even when the clock has
        // already run past it, which is the whole of BL-237. Testing only `_due > _clock` made
        // that a question about the STEP: an absolute ("Animation"/"Sequence") period shorter
        // than one step is already behind the clock by the time it is gated, so the iteration
        // read as instantaneous and collected the AnimFrame floor meant for untimed poll loops.
        // C3/M05's `ww_balmoral1/2/3` (LOOP 1000 @ Sequence 0.01 s, authored 10 s) took 16.7 s at
        // 60 Hz and 10.0 s at 600 Hz for exactly that reason — the floor, reached by a route the
        // floor's own comment says it cannot reach. An authored 0 still means "immediately after
        // the previous event", so the untimed poll idiom keeps its pacing and its double-poll guard.
        if (_due > _clock || ev.StartTime > 0f)
        {
            _iterScheduledTime = true;
        }
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
