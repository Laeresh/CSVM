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
    /// the call actually reached is still running, and the runner holds its next event until it
    /// reads false.
    ///
    /// <para>A predicate rather than a duration because that is what the mechanism is: the
    /// callee's own length is not knowable at the call (its sequences can call further
    /// sequences, and an SI script's run time lives in a separate archive). A closure rather
    /// than a "re-ask by name" call because the host must test the exact instances THIS call
    /// reached — a pooled template copy is chosen at dispatch, and asking again would take
    /// another pool slot.</para></summary>
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

    /// <summary>Seconds since this instance started — the original's <c>anim+0xb0</c>, one clock
    /// owned by the definition instance and shared by every sequence of it. A <c>START_TIME
    /// ANIMATION t</c> gates against THIS, not against the firing sequence's own clock, which is
    /// the whole difference for a sequence a later CALL_SEQUENCE started: the instance clock is
    /// already running when the runner's starts at zero. Never rewound — a LOOP rewinds only the
    /// sequence's timers. (The exe clamps it at 86,400 s inside the LOOP handler; a session never
    /// reaches a day, so the clamp is not reproduced.)</summary>
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
    /// state. The original resolves the name to an index in the definition's OWN sequence array and
    /// then does exactly one thing — <c>if (state == parked) state = running;</c> — so a call into a
    /// sequence that is already running is a silent no-op, and so is a call naming a sequence whose
    /// authored activation is not ON_CALL (such a sequence is never parked: it runs with the
    /// animation and stops done). Both no-ops are load-bearing in the shipped data: 77 definitions
    /// call one sequence from more than one site (`sonic_ground_effect` calls `sonic_light_seq`
    /// twice, `player` calls `destroy_craft` three times), and 6 call a non-ON_CALL sequence
    /// (`reflight1..6 → refinery_light_seq`).
    ///
    /// <para>⚠ Returns whether the definition HAS that sequence, not whether anything started —
    /// callers read it to decide whether the name resolved at all, and a no-op call must still
    /// report found or the CALL_ANIMATION fallback fires on a name that was there.</para></summary>
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
    /// nothing can put a sequence into flight behind the list's back. The bootstrap that starts a
    /// definition's non-ON_CALL sequences goes through here too, as do the death slot and the
    /// damage-stage host, which feed sequences that are not in <c>Def.Sequences</c> at all.</summary>
    public void AddRunner(AnimSequence seq)
    {
        Runners.Add(new SequenceRunner(seq, Clock));
    }

    /// <summary>STOP_SEQUENCE: halts every active runner named <paramref name="name"/> —
    /// including the caller's own runner (the data's break-out-of-my-own-IF-chain idiom) — and
    /// does nothing else. The original resolves the name exactly as <see cref="CallSequence"/>
    /// does and then unconditionally marks that sequence DONE; there is no start-if-not-running
    /// path anywhere in it. Since a call can only start a sequence from the PARKED state,
    /// stopping an ON_CALL sequence that was never called leaves it un-callable until the whole
    /// definition resets — a stop on a parked sequence is a DISABLE. 16 shipped definitions hit
    /// exactly that (`flame_ball_01`/`flame_ball_02` → `stop_p1trail`, in every chapter, inside
    /// the HE explosion's call chain), so the teardown those definitions name simply never runs.
    /// Decode in docs/formats/anim-definitions.md.
    ///
    /// <para>⚠ The DISABLE is not persisted: a halt here is not remembered, so a later
    /// CALL_SEQUENCE on the same name still starts the sequence, where the original's DONE state
    /// would refuse it until the definition resets. 123 definitions name one sequence in both a
    /// call and a stop (mostly `flame_light_seq`), but whether any of them reaches the stop
    /// BEFORE the call at run time is a control-flow question the static census cannot answer —
    /// persisting the flag would change all 123 on a divergence none of them is known to
    /// observe.</para>
    ///
    /// <para>⚠ Returns whether the name RESOLVED — a matching runner, or failing that a sequence
    /// of that name on the definition — never whether anything was halted. False means the name
    /// is not this definition's at all.</para></summary>
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
/// Executes one sequence's event list on a clock.
///
/// Scheduling (inferred from the data, recorded in docs/formats/anim-definitions.md):
/// an event's <c>start</c> gives an origin and a delay — "Animation" from the INSTANCE's
/// start (a clock shared by all its sequences, see <see cref="AnimInstance.Clock"/>),
/// "Sequence" from this sequence's start, "Event" from the previous event's
/// COMPLETION. An absent <c>start</c> encodes as "Animation + 0.0" and behaves as "as soon
/// as the previous event finishes", because the gate is only evaluated once the previous
/// event reports completion. That reading is what makes the C1 train work: its sequences are
/// [ObjectMotionSiScript, Loop{-1}] with no start offsets, and only "after the previous
/// event completes" turns that into the surveyed ~327 s track loop rather than a
/// zero-length infinite loop.
/// </summary>
public sealed class SequenceRunner
{
    /// <summary>One authored ANIMATION FRAME, in seconds — the unit a <c>LOOP</c> count is
    /// denominated in. A <c>LOOP n</c> over an instantaneous body can only advance one pass per
    /// engine update, so it is a timer of n updates; 1/60 s is where that update sits — measured
    /// against the original, not assumed. `ref_fueltanks`' <c>fire_n_smoke</c>
    /// (<c>LOOP 200</c>) burns ~3 s, giving 200/3 ≈ 60, and the same burn was then timed at 60 fps
    /// and at 120 fps: it took the SAME time at both. Per-rendered-frame ticking would have halved
    /// it at 120, so the original's sequence tick is decoupled from rendering and every untimed
    /// count is a real authored duration. See docs/formats/anim-definitions.md.
    ///
    /// <para>⚠ Deliberately its OWN constant, not <see cref="Utils.GameClock.FixedDt"/>, though the
    /// two are equal today. That equality is what keeps every `--det` capture byte-identical
    /// (one pass per fixed step) — but "the rate the
    /// original's artists counted frames at" and "the rate we step the simulation at" are
    /// independent facts. Re-stepping the sim at 1/120 for physics reasons must NOT halve every
    /// authored animation timer.</para></summary>
    public const float AnimFrame = 1f / 60f;

    private readonly AnimSequence _seq;
    // One entry per open IF: has any branch of that chain already run? An ELSEIF/ELSE
    // reached with the flag set is the *fall-through* off the end of a taken branch and
    // must skip to the ENDIF; reached with it clear, it is the next candidate to test.
    // The original carries no such state — it distinguishes the two by ARRIVAL: a branch
    // marker the stepper walked into is dispatched (fall-through), one a failed condition's
    // scan landed on is stepped past unread. This stack is our stand-in for that, not a
    // model of the original; see Scan for the one place the difference could show.
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
    /// re-tests Done after every dispatch, so a self-halt exits before the next event, and
    /// <see cref="AnimInstance.Advance"/>'s sweep removes the runner. Resources the sequence
    /// already launched (motions, puffers) are untouched: their lifetimes are authored
    /// independently and outlive the sequence that launched them. ⚠ One exception, at INSTANCE end
    /// rather than here: a motion still owing a BOUNCE_SEQUENCE holds its instance open, because
    /// the landing has to dispatch into one (<c>MotionSet.OwesBounce</c>).</summary>
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
                // WAIT_FOR_COMPLETION. The hold gates this sequence's NEXT event and
                // nothing else — it is not a lifetime hold on the runner, and the data is what
                // says so. Censused over both front-ends
                // (analysis/wait-for-completion/FINDINGS.md): of the 2,999 flagged calls the
                // runtime can reach, 2,770 are the LAST event of their block and 2,770 of those
                // name a callee that never terminates — the `sputter_fire`/`sputter_black_smoke`/
                // `sputter_fire_smoke`/`gen_drop_ladder` `LOOP{-1}` idiom. Not ONE flagged call
                // with an event behind it names a never-terminating callee, in either front-end.
                // So a runner-lifetime reading would wedge 2,770 authored sequences open forever
                // while a next-event reading is consistent with the whole install, zero
                // exceptions — which is also why the `_pc` test below is a rule and not a
                // shortcut: past the last event there is nothing to hold back, and the trailing
                // Done check retires the runner exactly as it always did.
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
                    // Original mechanism (004ebfd0): a u16 counter at +0x30 increments on
                    // every visit, THEN the visit terminates the loop if the counter now
                    // equals the authored count; an authored -1 is special-cased infinite
                    // regardless of the counter. Reading the count fresh off the event (not
                    // caching it) matches the exe, which re-reads its own event struct too.
                    //
                    // An authored 0 is infinite as a CONSEQUENCE of this shape, not a case
                    // of its own: the counter starts at 0 and is incremented BEFORE the
                    // compare, so after the first visit it only ever grows and can't equal 0
                    // again — the 26 ground-vehicle routes that ship Count 0 (surveyed across
                    // the whole install: C1's police/mafia/black_car/truck traffic, C2's and
                    // C3/M02's studebakers, every one the LAST event of its sequence) run
                    // continuously for exactly that reason, with no normalisation needed to
                    // say so. A real u16 wrap would re-hit 0 at pass 65,536; that wrap is not
                    // implemented — it is unreachable within a session and -1 already covers
                    // the deliberately-infinite case.
                    int authored = (int)(ev.Data.Num("value") ?? CountOf(ev) ?? -1f);
                    _loopPasses++;
                    if (authored != -1 && _loopPasses == authored)
                    {
                        _done = true;
                        break;
                    }
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
                    // level down. Measured over the install's 599 timed loops: a 0.02 s
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
            // sign's trailing `Loop {Event 1.2}` is its inter-cycle pause).
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

    // Gate the event at _pc on ITS OWN schedule.
    //
    // An event's START_TIME says when *that* event fires — see
    // AnimEvent.StartOffset: "Event" = since the previous event fired,
    // null = immediately after it. Do NOT read the offset off the event just FIRED
    // and apply it to its successor — that shifts **every sequence in the install**
    // by one slot: a timestamped event fires one slot early and its unstamped
    // partner one slot late.
    //
    // C1's `bowl` sign is the clean demonstration. Its compiled
    // sequence is nine strict `des_on`/`des_off` SWAP pairs plus an infinite Loop,
    // and only the FIRST of each pair carries a timestamp — the one-slot shift splits
    // every pair, leaving both variants lit at t=0 and then **nothing at all** for
    // each gap. Measured face-on at the sign: 38.0% of frames completely blank under
    // the shifted reading, 0% under this one — in-game the sign disables and
    // re-enables itself instead of flashing as the original does.
    //
    // Control-flow events (LOOP/IF/ELSEIF/…) do not advance _base —
    // they take no time — but they ARE gated, which is what gives the sign's trailing
    // `Loop {Event 1.2}` its inter-cycle pause (a Loop branch that hard-reset the
    // gate to zero would discard that offset outright).
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
            // Origin ANIMATION is the INSTANCE's clock (the original compares against anim+0xb0,
            // shared by every sequence of the definition), which is not this sequence's clock for
            // any sequence a CALL_SEQUENCE started after t=0 — 191 shipped events read the
            // difference, among them `ap_light_seq`'s LightAnimation chain and the 10 s puffer
            // shut-offs on every rocket/torpedo trail. Restated in this runner's own clock so a
            // single comparison still gates every origin: both clocks tick by the same dt, so the
            // gap between them is fixed from here to the fire, and a LOOP that rewinds _clock
            // re-gates through here anyway.
            "Animation" => _clock + (ev.StartTime - _animClock),
            // Origin SEQUENCE is seq+0x24 — absolute against this sequence's own start.
            "Sequence" => ev.StartTime,
            // "Event" (seq+0x28) and an ABSENT start alike measure from the previous event's
            // completion. ⚠ An absent start ENCODES as Animation + 0.0 and must NOT be routed
            // through the animation clock: it would gate the install's ~36k unstamped compiled
            // events on a clock that is already running. StartOffset is null for them, so they
            // land here; keep it that way.
            _ => _base + ev.StartTime,
        };
        // "Did the DATA schedule time?" — an authored offset counts even when the clock has
        // already run past it. Testing only `_due > _clock` made
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

    // The next ELSEIF/ELSE/ENDIF — where a FAILED condition continues. Lands ON the event,
    // so the loop re-dispatches it as the next candidate.
    private int NextBranch(int from) => Scan(from, stopAtElse: true);

    // The next ENDIF — where a branch that ran, or one skipped past its whole chain,
    // continues. Lands ON the ENDIF so it pops the frame.
    private int SkipToEnd(int from) => Scan(from, stopAtElse: false);

    // Walk forward to the event a branch jump lands on. ⚠ Deliberately NOT nesting-aware:
    // the original walks event by event and breaks on the FIRST byte in its stop set, with
    // no depth counter — a false IF stops at ELSE/ELSEIF/ENDIF alike, while the ELSE/ELSEIF
    // fall-through stops at ENDIF only. Those two stop sets are the
    // `stopAtElse` flag and must stay separate.
    //
    // This is observable, not academic. 48 shipped sequences nest — every chapter's
    // `gunhit-*slug_gunhit` / `mag_gunhit-*`, played on every gun impact — all in one
    // shape: `If lod / If range / If weight … Elseif weight … Else Endif / Else Endif /
    // Endif`. A false OUTER condition lands on the INNER chain's ELSEIF and re-tests it, so
    // the impact light still fires on its 20% roll with the LOD gate and the 1 km range gate
    // both failed. A depth counter skips the whole thing instead. That reads like a compiler
    // bug in the original and it is what the original does; do not "fix" it.
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
