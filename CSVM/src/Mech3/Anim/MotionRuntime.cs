using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// The full OBJECT_MOTION rigid body: a ballistic translate/launch, a scale ramp and a
/// tumble, driving one node over its run time, in the node's own parent frame (the same
/// absolute-in-parent-frame convention as <see cref="FromToMotion"/>). Each channel is
/// optional and an absent one holds the node's live value, seeded once at creation so the
/// motion cannot compound into itself. This is the reachable half of OBJECT_MOTION that the
/// old handler only counted — thrown by a crash or (in M3) a weapon hit; nothing ambient
/// fires it.
///
/// <para><b>Semantics</b> (established from <c>player_crash_dirt</c>'s pieces,
/// <c>call_crash_trails</c> and <c>flydirt</c>; ⚠ several are TUNE, not a settled decode):
/// <list type="bullet">
/// <item><c>translation.initial</c> is the launch VELOCITY (a piece leaves at y=10 m/s);
///   <c>rnd_xz</c> a per-axis random spread added to it (through the runtime's seedable
///   <c>_rng</c>, so a lab replay is deterministic); <c>delta</c> a velocity ramp over the
///   run time (change from initial to initial+delta) — 0 on every reachable piece, so its
///   exact reading is near-invisible.</item>
/// <item><c>translation_range</c> is a RANGED ballistic launch (the burning-debris arcs): a
///   random horizontal distance <c>xz</c> and vertical <c>y</c> travelled over the run time,
///   fired in a random azimuth — <c>vHoriz = xz/run_time</c>,
///   <c>vVert = y/run_time − ½·g·run_time</c> (so the arc reaches <c>y</c> at the end).
///   ⚠ undocumented and never simulated before; <c>initial</c>/<c>delta</c> are unmapped.</item>
/// <item><c>gravity.value</c> (negative) accelerates the launch; folded straight into the
///   constant acceleration. <c>do_intersections</c> ground-rest and the <c>bounce_sequence</c>
///   re-launch are a Layer-1.5 follow-up (they need a physics ray) — the body integrates
///   freely over the run time and then finishes.</item>
/// <item><c>forward_rotation.Time.initial</c> is a tumble RATE (rad/s) about the node's local
///   X axis (a piece = 15.708 = 900°/s). ⚠ the axis is a reasoned choice — the data carries a
///   scalar rate, not an axis — an end-over-end tumble about the local X reads well for
///   scattered wreckage.</item>
/// <item><c>xyz_rotation.initial</c> a steady multi-axis spin (rad/s), composed like
///   <see cref="SpinMotion"/>; present only on the rare spin+ballistic events.</item>
/// <item><c>scale.initial</c> a start scale and <c>scale.delta</c> the change over the run
///   time, a linear ramp (the dust: (3.5,10,3.5) → (2.5,5,2.5) over 6 s). Absolute, like
///   <c>PoseScale</c>, so it replaces the held scale rather than multiplying it.</item>
/// </list></para>
/// </summary>
internal sealed class MotionRuntime : IAnimMotion
{
    // Held pose, seeded once from the live transform: an absent channel carries it through.
    private Basis _heldRot;      // orthonormal; scale kept out

    private Vector3 _heldScale;

    private Vector3 _heldOrigin;

    // Ballistic: origin(t) = held + v0·t + ½·accel·t²  (accel folds gravity + any velocity ramp).
    private Vector3 _v0, _accel;

    private bool _hasBallistic;

    // Scale ramp: scale(t) = init + delta·u,  u = t/run_time clamped.
    private Vector3 _scaleInit, _scaleDelta;

    private bool _hasScale;

    private float _tumbleRate;   // rad/s about local X (forward_rotation)

    private Vector3 _spinRate;   // rad/s per local axis (xyz_rotation)

    private float _t, _runTime;

    public Node3D Target { get; private init; } = null!;

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _t >= _runTime;

    public static MotionRuntime? Create(AnimRuntime rt, Node3D target, AnimData data, float runTime)
    {
        var rest = rt.RestOf(target); // records the authored pose; the fallback for a bad live basis
        var held = target.Transform;
        float det = held.Basis.Determinant();
        if (!float.IsFinite(det) || Mathf.Abs(det) < 1e-9f || !held.Origin.IsFinite())
            held = rest;

        float rtSafe = Mathf.Max(runTime, 0f);
        var m = new MotionRuntime
        {
            Target = target,
            _heldRot = held.Basis.Orthonormalized(),
            _heldScale = held.Basis.Scale,
            _heldOrigin = held.Origin,
            _runTime = rtSafe,
        };

        float RandSym() => (float)(rt._rng.NextDouble() * 2.0 - 1.0); // [-1, 1] via the seedable RNG
        float Rand(float a, float b) => a + (float)rt._rng.NextDouble() * (b - a);

        float gravity = data.Obj("gravity")?.Num("value") ?? 0f;

        // The plane's momentum (world-space), carried by the launched pieces so they scatter
        // along its travel instead of just popping up in place. Converted into the node's parent
        // frame, where the launch velocity lives (v0 drives Target.Transform, a local pose).
        Vector3 InheritedLocal()
        {
            if (rt.InheritedWorldVelocity == Vector3.Zero)
                return Vector3.Zero;
            var parentBasis = (target.GetParent() as Node3D)?.GlobalTransform.Basis ?? Basis.Identity;
            return parentBasis.Inverse() * rt.InheritedWorldVelocity;
        }

        if (data.Obj("translation") is { } tr)
        {
            var v0 = tr.Vec3("initial");
            var rnd = tr.Vec3("rnd_xz");
            v0 += new Vector3(RandSym() * rnd.X, RandSym() * rnd.Y, RandSym() * rnd.Z);
            var delta = tr.Vec3("delta");
            // delta ramps velocity over run_time → a constant acceleration of delta/run_time.
            var rampAccel = rtSafe > 0f ? delta / rtSafe : Vector3.Zero;
            m._v0 = v0 + InheritedLocal();
            m._accel = rampAccel + new Vector3(0f, gravity, 0f);
            m._hasBallistic = true;
        }
        else if (data.Obj("translation_range") is { } range)
        {
            float horiz = Rand(range.Obj("xz")?.Num("min") ?? 0f, range.Obj("xz")?.Num("max") ?? 0f)
                          / Mathf.Max(rtSafe, 0.1f);
            float vVert = Rand(range.Obj("y")?.Num("min") ?? 0f, range.Obj("y")?.Num("max") ?? 0f)
                          / Mathf.Max(rtSafe, 0.1f)
                          - 0.5f * gravity * rtSafe; // gravity < 0 → the second term adds launch speed
            float azimuth = Rand(0f, Mathf.Tau);
            m._v0 = new Vector3(Mathf.Cos(azimuth) * horiz, vVert, Mathf.Sin(azimuth) * horiz)
                    + InheritedLocal();
            m._accel = new Vector3(0f, gravity, 0f);
            m._hasBallistic = true;
        }

        if (data.Obj("scale") is { } sc)
        {
            m._scaleInit = sc.Vec3("initial");
            m._scaleDelta = sc.Vec3("delta");
            m._hasScale = m._scaleInit.LengthSquared() > 1e-9f;
        }

        // forward_rotation.Time.initial is a TOTAL angle over run_time (the `Time`
        // parameterization), not a rate: the crash pieces carry 5π and 4.44π (clean multiples of
        // π), which read as a rate spin at ~15 rad/s (900°/s) — "spins like crazy" (user
        // playtest). ÷ run_time gives 5π over 6 s = 2.5 tumbles, the reference debris tumble.
        float fwdTotal = data.Obj("forward_rotation")?.Obj("Time")?.Num("initial") ?? 0f;
        m._tumbleRate = rtSafe > 0f ? fwdTotal / rtSafe : 0f;
        m._spinRate = data.Obj("xyz_rotation")?.Vec3("initial") ?? Vector3.Zero;

        // Nothing to drive → no motion (a bare gravity/bounce stub, handled by the caller).
        bool any = m._hasBallistic || m._hasScale || m._tumbleRate != 0f || !m._spinRate.IsZeroApprox();
        return any ? m : null;
    }

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = t;
        float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);
        // Clamp the ballistic clock too so a finished body holds its last pose (it is removed
        // the frame it finishes, but Seek can be called past run_time by an instant land).
        float tb = _runTime > 0f ? Mathf.Min(t, _runTime) : t;

        var origin = _heldOrigin;
        if (_hasBallistic)
            origin = _heldOrigin + _v0 * tb + 0.5f * tb * tb * _accel;

        var basis = _heldRot;
        if (_tumbleRate != 0f)
            basis = basis.Rotated(basis.X.Normalized(), _tumbleRate * tb);
        if (!_spinRate.IsZeroApprox())
        {
            var a = _spinRate * tb;
            if (a.X != 0f) basis = basis.Rotated(basis.X.Normalized(), a.X);
            if (a.Y != 0f) basis = basis.Rotated(basis.Y.Normalized(), a.Y);
            if (a.Z != 0f) basis = basis.Rotated(basis.Z.Normalized(), a.Z);
        }

        var scale = _hasScale ? _scaleInit + _scaleDelta * u : _heldScale;

        Target.Transform = new Transform3D(basis.Scaled(scale), origin);
    }
}
