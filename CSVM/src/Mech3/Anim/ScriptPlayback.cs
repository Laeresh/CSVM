using System;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>Plays a compiled SI script onto a node: per-frame cubics for translation and
/// the half-angle quaternion composition for rotation (docs/formats/anim-definitions.md).
/// Loops when the owning sequence loops — the runner restarts it.</summary>
internal sealed class ScriptPlayback : IAnimMotion
{
    // A scripted node's smoothness is a per-step question, and --debug-anim's once-a-second line
    // cannot answer it. Naming a node here logs its pose and the step's own dt every tick, so a
    // hand-off discontinuity, a per-step oscillation and a stuttering clock separate in one run.
    private static readonly string? TraceNode =
        System.Environment.GetEnvironmentVariable("CSVM_TRACE_SISCRIPT");

    private readonly SiScript _script;

    private readonly bool _trace;

    private ulong _traceWallUsec;

    private ulong _traceFrames;

    private float _traceSim;

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
        _trace = TraceNode != null && target.Name.ToString().Contains(TraceNode, StringComparison.OrdinalIgnoreCase);
        var rest = rt.RestOf(target);
        _rot = rest.Basis.Orthonormalized();
        _scale = rest.Basis.Scale;
        _origin = rest.Origin;
        Seek(0f);
    }

    public Node3D Target { get; }

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _t >= _script.Duration;

    public void Tick(float dt)
    {
        if (_trace)
        {
            _traceSim += dt;
            var prev = Target.GlobalPosition;
            ulong wall = Time.GetTicksUsec();
            Seek(_t + dt);
            var now = Target.GlobalPosition;
            double wallMs = _traceWallUsec == 0UL ? 0d : (wall - _traceWallUsec) / 1000d;
            _traceWallUsec = wall;
            ulong drawn = (ulong)Engine.GetFramesDrawn();
            ulong shown = _traceFrames == 0UL ? 0UL : drawn - _traceFrames;
            _traceFrames = drawn;
            var euler = Target.GlobalBasis.GetEuler();
            float step = prev.DistanceTo(now);
            Log.Debug("anim", $"sitrace node={Target.Name} def={Owner.Def?.AnimName} sim={_traceSim:F4} dt={dt:F5} wall_ms={wallMs:F3} drawn={shown} t={_t:F4} step={step:F4} pos={now.X:F4},{now.Y:F4},{now.Z:F4} rot={euler.X:F6},{euler.Y:F6},{euler.Z:F6} scale={_scale.X:F6}");
            return;
        }

        Seek(_t + dt);
    }

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
