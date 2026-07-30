using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>A steady spin about the node's own axes at a fixed rate (OBJECT_MOTION's
/// XYZ_ROTATION), which is what turns the zeppelin nacelle props and the rotating signs.
/// Endless unless the event gave a RUN_TIME — 580 of the 590 reachable spins are endless,
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

    /// <summary>Whether an incoming registration is this same spin, so a looping sequence
    /// re-asserting it can be left alone instead of restarted.</summary>
    public bool Matches(Vector3 rate, float runTime) =>
        _rate.IsEqualApprox(rate) && Mathf.IsEqualApprox(_runTime, runTime);

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = _runTime > 0f ? Mathf.Min(t, _runTime) : t;
        var a = _rate * _t;
        // Applied X→Y→Z about the local axes. Order is only observable when two axes spin
        // at once, which nothing reachable in this install does (every reached event is
        // single-axis); recheck this if a multi-axis spin ever turns up looking wrong.
        var b = _rest;
        if (a.X != 0f) b = b.Rotated(b.X.Normalized(), a.X);
        if (a.Y != 0f) b = b.Rotated(b.Y.Normalized(), a.Y);
        if (a.Z != 0f) b = b.Rotated(b.Z.Normalized(), a.Z);
        var xf = Target.Transform;
        xf.Basis = b;
        Target.Transform = xf;
    }
}
