using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>A steady spin about the node's own axes at a fixed rate (OBJECT_MOTION's
/// XYZ_ROTATION), which is what turns the zeppelin nacelle props and the rotating signs.
/// Endless unless the event gave a RUN_TIME, 580 of the 590 reachable spins are endless,
/// so `Finished` staying false forever is the normal case, not a leak. Rotation is applied
/// about the LOCAL axes like every other prop in this project (PropAnimator does the same
/// for the player's aircraft), and accumulated from a stored rest pose rather than
/// integrated per frame so a long session cannot drift.</summary>
internal sealed class SpinMotion : IAnimMotion
{
    private readonly Basis _rest;

    private readonly Vector3 _rate; // radians/second, local axes

    private readonly float _runTime; // 0 = endless

    private float _t;

    // ⚠ Rest comes from the LIVE pose, never from RestOf. The original holds no rest pose for a
    // spin at all: it adds rate·dt onto the node's own euler angles every frame, so a replacing
    // event continues from wherever the node is (docs/org/objectMotion.md). Seeding the authored
    // pose here would snap the node back at every rate change, which the original never does.
    public SpinMotion(Node3D target, Vector3 rate, float runTime)
    {
        Target = target;
        _rest = target.Transform.Basis;
        _rate = rate;
        _runTime = runTime;
        Seek(0f);
    }

    public Node3D Target { get; }

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _runTime > 0f && _t >= _runTime;

    /// <summary>Whether this spin has no authored end, <c>run_time ?? 0f</c>, which
    /// <see cref="Finished"/> never satisfies. Read by <see cref="MotionSet.HoldsTemplate"/>, which
    /// must not let one pin a template revealed for the session.</summary>
    public bool Endless => _runTime <= 0f;

    /// <summary>Whether an incoming registration is this same spin, so a looping sequence
    /// re-asserting it can be left alone instead of restarted.</summary>
    public bool Matches(Vector3 rate, float runTime) =>
        _rate.IsEqualApprox(rate) && Mathf.IsEqualApprox(_runTime, runTime);

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = _runTime > 0f ? Mathf.Min(t, _runTime) : t;
        var xf = Target.Transform;
        xf.Basis = ComposeSpin(_rest, _rate, _t);
        Target.Transform = xf;
    }

    /// <summary>Rotates <paramref name="rest"/> by <paramref name="rateRadPerSec"/>·<paramref name="t"/>
    /// about the node's own local axes, the accumulate-from-rest math this class applies per frame,
    /// exposed so <c>PropAnimator</c> can spin the flying aircraft's prop/rotor discs through the same
    /// decode instead of stepping a separate <c>RotateObjectLocal</c> loop (which drifts over a long
    /// session; this recomputes an absolute pose every call instead of integrating one).</summary>
    internal static Basis ComposeSpin(Basis rest, Vector3 rateRadPerSec, float t)
    {
        var a = rateRadPerSec * t;
        // Applied X→Y→Z about the local axes. Order is only observable when two axes spin
        // at once, which nothing reachable in this install does (every reached event is
        // single-axis); recheck this if a multi-axis spin ever turns up looking wrong.
        var b = rest;
        if (a.X != 0f) b = b.Rotated(b.X.Normalized(), a.X);
        if (a.Y != 0f) b = b.Rotated(b.Y.Normalized(), a.Y);
        if (a.Z != 0f) b = b.Rotated(b.Z.Normalized(), a.Z);
        return b;
    }
}
