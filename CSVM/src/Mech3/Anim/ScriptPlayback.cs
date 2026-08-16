using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>Plays a compiled SI script onto a node: per-frame cubics for translation and
/// the half-angle quaternion composition for rotation (docs/formats/anim-definitions.md).
/// Loops when the owning sequence loops — the runner restarts it.</summary>
internal sealed class ScriptPlayback : IAnimMotion
{
    private readonly SiScript _script;

    // Rotation, translation and scale, held as three separate running components seeded from
    // rest rather than read back from the live transform, so a scale-only frame cannot
    // compound into an already-scaled basis. See Seek for why they stay apart.
    private Basis _rot;

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

    // ⚠ Do not collapse the three components into one rest transform. An absent channel means
    // hold the last value this script wrote, not return to rest.
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
