using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// An OBJECT_MOTION_FROM_TO tween: linear over the authored run time, in the node's own parent
/// frame. Decode: docs/formats/anim-definitions.md.
/// ⚠ Channels are absolute poses in that frame, never offsets from the rest pose. Adding to rest
/// doubles every world position.
/// ⚠ An absent channel means hold the last live value, not the rest pose. Read as rest, C1's
/// traffic drives mis-headed.
/// ⚠ Do not read the compiled <c>*_delta</c> channels. Each is the sibling channel's rate, not
/// an extra offset, and composing it doubles the motion's speed.
/// Rotations arrive here as radians; <see cref="AnimDefs"/> converts reader degrees at parse.
/// </summary>
internal sealed class FromToMotion : IAnimMotion
{
    // The pose this event starts from, as components: an absent channel carries its
    // component through untouched. Orthonormal rotation with the scale kept out of it,
    // exactly as ScriptPlayback holds them.
    private Basis _heldRot;

    private Vector3 _heldEuler;   // _heldRot as Yxz euler, for the missing-FROM fallback

    private Vector3 _heldScale;

    private Vector3 _heldOrigin;

    private Vector3? _tFrom, _tTo, _rFrom, _rTo, _sFrom, _sTo;

    private float _t, _runTime;

    public Node3D Target { get; private init; } = null!;

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _t >= _runTime;

    private bool HasAnyChannel => _tTo != null || _rTo != null || _sTo != null;

    public static FromToMotion? Create(AnimRuntime rt, Node3D target, AnimData data, float runTime)
    {
        // RestOf is still called for its side effect — it records the authored pose the
        // first time anything touches the node, which PoseRotate/PoseScale read back — and
        // as the fallback when the live pose is unusable.
        var rest = rt.RestOf(target);
        var held = target.Transform;
        float det = held.Basis.Determinant();
        if (!float.IsFinite(det) || Mathf.Abs(det) < 1e-9f || !held.Origin.IsFinite())
        {
            // A blown-up or singular live basis would poison every later event on this
            // node; the authored pose is the only sane thing left to hold.
            held = rest;
        }
        var rot = held.Basis.Orthonormalized();
        var m = new FromToMotion
        {
            Target = target,
            _heldRot = rot,
            _heldEuler = rot.GetEuler(EulerOrder.Yxz),
            _heldScale = held.Basis.Scale,
            _heldOrigin = held.Origin,
            _runTime = Mathf.Max(runTime, 0f),
        };
        (m._tFrom, m._tTo) = Channel(data, "translate");
        (m._rFrom, m._rTo) = Channel(data, "rotate");
        (m._sFrom, m._sTo) = Channel(data, "scale");
        return m.HasAnyChannel ? m : null;
    }

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = t;
        float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);

        // Each channel either drives its component or leaves the held value alone.
        var origin = _heldOrigin;
        if (_tTo is { } tTo)
            origin = (_tFrom ?? _heldOrigin).Lerp(tTo, u);

        var rot = _heldRot;
        if (_rTo is { } rTo)
            rot = Euler((_rFrom ?? _heldEuler).Lerp(rTo, u));

        var scale = _heldScale;
        if (_sTo is { } sTo)
            scale = (_sFrom ?? _heldScale).Lerp(sTo, u);

        Target.Transform = new Transform3D(rot.Scaled(AnimRuntime.NonSingularScale(scale)), origin);
    }

    private static (Vector3?, Vector3?) Channel(AnimData data, string name)
    {
        var ch = data.Obj(name);
        if (ch == null)
            return (null, null);
        return (ch.Has("from") ? ch.Vec3("from") : null,
                ch.Has("to") ? ch.Vec3("to") : null);
    }

    private static Basis Euler(Vector3 radians) => Basis.FromEuler(radians, EulerOrder.Yxz);
}
