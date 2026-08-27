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
    AnimDefinition Def, Node3D? Anchor, string Bounce, Node3D Target, bool ByContact);

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

    /// <summary>Contact-tested bodies that landed on a collider, vs ran their clock out. Only
    /// bodies that TEST contact are counted, so a zero after a tested launch is a real failure,
    /// not a query finding nothing.
    /// ⚠ Not zeroed by <see cref="Reset"/>, same rule as <see cref="LaunchCount"/>.</summary>
    public int ContactLandings { get; private set; }

    public int ClockEndings { get; private set; }

    /// <summary>The same tally split by tier, the default ground column against the authored
    /// <c>do_intersections</c> sweep. ⚠ The split is what makes the pair readable: the sweep's 166
    /// events can report a healthy-looking total on their own, so a combined counter cannot tell a
    /// working default tier from an inert one. Same <see cref="Reset"/> rule as the totals.</summary>
    public int ColumnLandings { get; private set; }

    public int ColumnClockEndings { get; private set; }

    public int SweepLandings { get; private set; }

    public int SweepClockEndings { get; private set; }

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
            // ⚠ A motion outlives the node it drives: an aircraft freed mid-animation (an airframe
            // swap, a rig torn down) leaves one here, and writing a transform to a disposed object
            // throws out of the whole runtime advance rather than losing one motion.
            if (!GodotObject.IsInstanceValid(_motions[i].Target))
            {
                _motions.RemoveAt(i);
                continue;
            }

            _motions[i].Tick(dt);
            if (!_motions[i].Finished)
                continue;
            var done = _motions[i];
            _motions.RemoveAt(i);
            if (done is MotionRuntime { TestsContact: true } tested)
            {
                bool column = tested.ContactTier == MotionContactTier.Column;
                if (tested.LandedByContact)
                {
                    ContactLandings++;
                    if (column)
                        ColumnLandings++;
                    else
                        SweepLandings++;
                }
                else
                {
                    ClockEndings++;
                    if (column)
                        ColumnClockEndings++;
                    else
                        SweepClockEndings++;
                }
            }

            // BOUNCE_SEQUENCE fires the def's own sparkoutN elsewhere; this only reports the
            // landing, it does not act on it.
            if (done is MotionRuntime { PendingBounce: { } bounce })
            {
                landed ??= new List<Landing>();
                landed.Add(new Landing(done.Owner.Def, done.Owner.Anchor, bounce, done.Target,
                    done is MotionRuntime { LandedByContact: true }));
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

    /// <summary>Whether an instance still owes a <c>BOUNCE_SEQUENCE</c>: the retirement hold
    /// <see cref="AnimRuntime.Retirable"/> consults (this module's docs/architecture.md entry). A
    /// bounce-terminated launch's runner finishes before the piece lands, so the instance needs
    /// this to stay open that long.
    /// ⚠ Deliberately narrow: a motion OWING a bounce, not any live motion. An unbounded
    /// <see cref="SpinMotion"/> never finishes, so pinning on any live motion makes it immortal.</summary>
    public bool OwesBounce(AnimDefinition def, Node3D? anchor) =>
        _motions.Any(m => m is MotionRuntime { PendingBounce: not null }
                          && m.Owner.Def == def && m.Owner.Anchor == anchor);

    /// <summary>Is some live motion already driving this node's transform? A launch onto such a
    /// node is a TAKEOVER, not a fresh throw — see <see cref="MotionRuntime.Create"/>'s re-home
    /// rule. Asked before <see cref="Add"/> evicts the incumbent, which is the only moment the
    /// answer exists.</summary>
    public bool DrivesTransform(Node3D target) =>
        _motions.Any(m => m.Target == target && m.Channel == MotionChannel.Transform);

    /// <summary>Whether this exact spin is already running, so a <c>Loop{-1}</c> sequence
    /// re-asserting it is left alone instead of rebuilt. Checked before <see cref="Add"/>, whose
    /// evict-then-insert would otherwise treat every re-assert as a fresh launch.
    /// ⚠ A needless rebuild restarts the clock at 0 every frame, so the prop looks driven in the
    /// logs while sitting almost still.</summary>
    public bool HasSpinOn(Node3D target, Vector3 rate, float runTime) =>
        _motions.Any(m => m.Target == target && m is SpinMotion s && s.Matches(rate, runTime));
}
