using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>A timed translucency fade (OBJECT_OPACITY_FROM_TO): lerp the subtree's opacity
/// from one value to another over the run time, through the same per-instance
/// <c>csky_opacity</c> shader parameter <see cref="AnimRuntime.SetSubtreeOpacity"/> writes. This is the
/// opacity channel — it does not touch the transform — so it coexists with a transform motion
/// on the same node (see <see cref="MotionChannel"/>). The endpoints are literal opacity
/// values (the endpoint `state` flag does not invert them; see the dispatch case), so no rest
/// pose is needed: the two numbers fully determine the fade.</summary>
internal sealed class OpacityFade : IAnimMotion
{
    private readonly AnimRuntime _rt;

    private readonly float _from, _to, _runTime;

    private float _t;

    public OpacityFade(AnimRuntime rt, Node3D target, float from, float to, float runTime)
    {
        _rt = rt;
        Target = target;
        _from = from;
        _to = to;
        _runTime = Mathf.Max(runTime, 0f);
        Seek(0f);
    }

    public Node3D Target { get; }

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public MotionChannel Channel => MotionChannel.Opacity;

    public bool Finished => _t >= _runTime;

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = t;
        float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);
        _rt.SetSubtreeOpacity(Target, Mathf.Lerp(_from, _to, u));
    }
}
