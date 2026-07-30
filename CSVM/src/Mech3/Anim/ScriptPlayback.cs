using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>Plays a compiled SI script onto a node: per-frame cubics for translation and
/// the half-angle quaternion composition for rotation (docs/formats/anim-definitions.md).
/// Loops when the owning sequence loops — the runner restarts it.
///
/// <para>Rotation, translation and scale are held as three SEPARATE running components
/// seeded from the node's authored rest pose, not read back out of the live transform.
/// Both halves of that matter. Reading the live basis made this the one transform writer
/// that could compound: a frame carrying <c>scale</c> but no <c>rotate</c> multiplied its
/// factor into an already-scaled basis every single frame, and a looping sequence
/// re-registering the playback re-entered at the blown-up pose (207 such frames across 12
/// scripts, all of them the C1 zeppelins). Keeping the components apart is what makes
/// `Seek(t)` a pure function of `t` rather than of call history. But they must be
/// components rather than one rest transform, because an ABSENT channel means "hold the
/// last value this script wrote", not "return to rest": C1/M04's `piratezep` sets its
/// orientation once in frame 0 and then ships 47 translate-only frames that must keep
/// it.</para></summary>
internal sealed class ScriptPlayback : IAnimMotion
{
    private readonly SiScript _script;

    private Basis _rot;      // orthonormal; the scale is kept out of it on purpose

    private Vector3 _scale;

    private Vector3 _origin;

    private float _t;

    public ScriptPlayback(AnimRuntime rt, Node3D target, SiScript script)
    {
        Target = target;
        _script = script;
        var rest = rt.RestOf(target);
        _rot = rest.Basis.Orthonormalized();
        _scale = rest.Basis.Scale;
        _origin = rest.Origin;
        Seek(0f);
    }

    public Node3D Target { get; }

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _t >= _script.Duration;

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = t;
        if (_script.FrameAt(Mathf.Min(t, _script.Duration)) is not { } frame)
            return;
        float dt = Mathf.Max(0f, t - frame.StartTime);
        if (frame.Rotate != null)
            _rot = new Basis(frame.Rotate.At(dt));
        if (frame.Translate != null)
            _origin = frame.Translate.At(dt);
        if (frame.Scale != null && frame.Scale.At(dt) is { } s && s.IsFinite() && s.LengthSquared() > 1e-9f)
            _scale = s;
        Target.Transform = new Transform3D(_rot.Scaled(_scale), _origin);
    }
}
