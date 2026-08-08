using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>A body that has come back down owing its <c>BOUNCE_SEQUENCE</c>: the sequence name and
/// the <c>(def, anchor)</c> instance it belongs to, plus the node for the debug line.
/// <see cref="MotionSet.Tick"/> has already removed the motion before it returns one of these, so a
/// landing can never name a body that is still in the collection.</summary>
internal readonly record struct Landing(
    AnimDefinition Def, Node3D? Anchor, string Bounce, Node3D Target);

/// <summary>The runtime's live motions and the two rules that govern registering one: an owner
/// stamp, and one motion per <c>(Target, Channel)</c>. Also answers the pending-bounce question the
/// instance walk retires on. Holds <see cref="Node3D"/> references but dereferences none of them —
/// every operation here is identity comparison, so the collection's behaviour is engine-free even
/// though its type is not.</summary>
internal sealed class MotionSet
{
    private readonly List<IAnimMotion> _motions = new();

    /// <summary>Live motions, for <c>LogMotions</c>' truncated list. Cumulative
    /// <see cref="LaunchCount"/>, not this, is the headless answer to whether a launch happened.</summary>
    public IReadOnlyList<IAnimMotion> Live => _motions;

    public int Count => _motions.Count;

    /// <summary>Running count of ballistic <see cref="MotionRuntime"/> bodies registered.
    /// ⚠ <see cref="Reset"/> deliberately leaves it standing — every reader takes a delta across an
    /// event, so zeroing it here would make a crash respawn read as a negative launch count.</summary>
    public int LaunchCount { get; private set; }

    /// <summary>How many <c>do_intersections</c> bodies ended on a collider, and how many ran their
    /// authored clock out instead. Only bodies that actually TEST contact are counted, so the pair
    /// answers one question: is the sweep doing anything? A <c>--fly</c> session that launched
    /// flagged bodies and reports <see cref="ContactLandings"/> 0 is the failure mode worth
    /// catching — the query silently finding nothing looks exactly like the old behaviour.
    /// ⚠ Left standing by <see cref="Reset"/>, the same rule (and for the same reason) as
    /// <see cref="LaunchCount"/>: every reader takes a delta across an event.</summary>
    public int ContactLandings { get; private set; }

    public int ClockEndings { get; private set; }

    /// <summary>Registers a motion, replacing any motion already driving the same node's channel. An
    /// object has exactly one motion in the original, and the data relies on it: C1/IA1's
    /// startanims run `hangar3_doors` (doors to ±50) and then `mp_hangar3_open` (the same
    /// doors to ±25), where the later one is meant to win. Without this both tween the same
    /// node every frame and the outcome depends on list order.</summary>
    public void Add(IAnimMotion motion, AnimDefinition def, Node3D? anchor)
    {
        motion.Owner = (def, anchor);
        // Evict only a prior motion on the SAME channel: a node can carry one transform motion
        // AND one opacity fade at once (the crash dust scales via a MotionRuntime while an
        // OpacityFade fades it), and those write different data, so neither displaces the other.
        _motions.RemoveAll(m => m.Target == motion.Target && m.Channel == motion.Channel);
        _motions.Add(motion);
        if (motion is MotionRuntime)
            LaunchCount++;
    }

    /// <summary>Advances every live motion by <paramref name="dt"/>, removes the finished ones, and
    /// returns those that owed a <c>BOUNCE_SEQUENCE</c> — empty on almost every frame. The caller
    /// dispatches them; this class holds no runtime back-reference, and returning them rather than
    /// calling out makes remove-before-dispatch structural instead of a rule to remember.</summary>
    public IReadOnlyList<Landing> Tick(float dt)
    {
        List<Landing>? landed = null;
        for (int i = _motions.Count - 1; i >= 0; i--)
        {
            _motions[i].Tick(dt);
            if (!_motions[i].Finished)
                continue;
            var done = _motions[i];
            _motions.RemoveAt(i);
            if (done is MotionRuntime { TestsContact: true } tested)
            {
                if (tested.LandedByContact)
                    ContactLandings++;
                else
                    ClockEndings++;
            }

            // BOUNCE_SEQUENCE: the piece has come back down, which for a bounce-terminated
            // launch is the whole meaning of its flight ending. The named sequence
            // belongs to the same definition — `sparkoutN` deactivates the piece and pops its
            // fireball, and that OBJECT_ACTIVE_STATE is what stops the trail through the
            // EndSustainedOn path, with no effects-side change here.
            if (done is MotionRuntime { PendingBounce: { } bounce })
            {
                landed ??= new List<Landing>();
                landed.Add(new Landing(done.Owner.Def, done.Owner.Anchor, bounce, done.Target));
            }
        }

        return (IReadOnlyList<Landing>?)landed ?? Array.Empty<Landing>();
    }

    /// <summary>Drops the motions a stopped instance registered, attributed to the exact
    /// <c>(def, anchor)</c> that registered them.</summary>
    public void DiscardFor(AnimDefinition def, Node3D? anchor) =>
        _motions.RemoveAll(m => m.Owner.Def == def && m.Owner.Anchor == anchor);

    /// <summary>Drops every live motion — the crash rig's respawn. Leaves
    /// <see cref="LaunchCount"/> alone.</summary>
    public void Reset() => _motions.Clear();

    /// <summary>Whether an instance still owes a <c>BOUNCE_SEQUENCE</c>, which holds it open past
    /// the end of its runners. Ordinarily "finished" means an instance's runners have all ended —
    /// but a bounce-terminated launch is the last event of its sequence, so the runner is done the
    /// frame the piece leaves the ground while the flight has seconds to run, and the landing needs
    /// a live instance to dispatch into. Measured without this hold: killing C1's seven
    /// <c>m_build</c> at once, one instance in seven lost its <c>part4</c> bounce — and which one
    /// depends on the randomised launch draw, so it is an intermittent miss, not a fixed one.
    ///
    /// <para>⚠ Deliberately narrow: a motion OWING A BOUNCE, not any live motion.
    /// <see cref="SpinMotion.Finished"/> is <c>_runTime > 0f &amp;&amp; _t >= _runTime</c> and the
    /// OBJECT_MOTION handler builds spins with <c>run_time ?? 0f</c>, so an unbounded steady spin
    /// is never finished — 2,181 of them across 1,037 definition files. Pinning on those would make
    /// every one of their instances immortal and break end-of-instance emitter teardown install-wide.
    /// A pending bounce self-expires; a spin does not.</para></summary>
    public bool OwesBounce(AnimDefinition def, Node3D? anchor) =>
        _motions.Any(m => m is MotionRuntime { PendingBounce: not null }
                          && m.Owner.Def == def && m.Owner.Anchor == anchor);

    /// <summary>Whether this exact spin is already running on the node, so a looping sequence
    /// re-asserting it can be left alone instead of restarted. The other half of the registration
    /// rule, and it cannot fold into <see cref="Add"/>: the guard has to run before the
    /// <see cref="SpinMotion"/> is constructed, since that constructor writes
    /// <c>Target.Transform</c>. These sit inside `Loop{-1}` sequences, so an already-turning prop
    /// would otherwise be rebuilt every frame — each rebuild re-reading rest from the current pose
    /// and restarting the clock at 0, which advances one frame's worth of angle and then throws it
    /// away. The prop would sit almost still while looking, in the logs, perfectly driven.</summary>
    public bool HasSpinOn(Node3D target, Vector3 rate, float runTime) =>
        _motions.Any(m => m.Target == target && m is SpinMotion s && s.Matches(rate, runTime));
}
