using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// An OBJECT_MOTION_FROM_TO tween: linear over the authored run time, in the node's own
/// parent frame.
///
/// The <c>translate</c>/<c>rotate</c>/<c>scale</c> channels are ABSOLUTE poses in that
/// frame, not offsets from the rest pose — verified across all 8 chapters: C1's
/// <c>mafia</c> car moves from (-6796, 128, -5958), which is its authored node translate
/// to within a metre, and C5's <c>m_gerter</c> crane hook moves between (21.3, 19.9,
/// -20.2) and (21.3, 7.9, -20.2) in its parent crane's frame. Adding these to the rest
/// pose doubles every world-space position (the C1 traffic ended up 7 km off the map).
/// Nodes whose animation is what places them — C2's <c>sailboat2</c> rests at its parent's
/// origin — simply don't match their rest pose, which is why "does it match the rest pose"
/// is a bad test and absolute-in-parent-frame is the rule.
///
/// <para>An ABSENT channel means "HOLD the last value written to this node", NOT "return to
/// the authored rest pose" — the same rule <c>ScriptPlayback</c> above documents, and for
/// the same reason. The pose is therefore held as three separate components seeded from the
/// node's LIVE transform at the moment the event fires, not from <c>RestOf</c>. Reading the
/// rest pose instead is what made C1's traffic drive mis-headed: <c>police_chase</c> sets
/// <c>suspect</c>/<c>police_car</c> to 45° with an OBJECT_ROTATE_STATE and then plays a
/// translate-only 2 s leg, which snapped both cars back to 0° for the diagonal; the same
/// def parks <c>suspect</c> for 27 s on a translate-only event that must hold -110°.
/// Surveyed install-wide: 883 of 1,802 OBJECT_MOTION_FROM_TO events carry no rotate
/// channel, and on 89 of them (26 nodes — the C1 traffic and firetrucks, C2's ten
/// studebakers, its sailboats and yachts) the value to hold differs from the authored rest,
/// up to <c>sailboat1</c>'s 300 s leg held 180° out. The components are kept SEPARATE
/// rather than as one transform for `ScriptPlayback`'s reason: it keeps <c>Seek(t)</c> a
/// pure function of <c>t</c>, and a rotate channel can no longer silently discard the
/// node's scale. They are seeded ONCE per event rather than re-read per frame, so a tween
/// cannot compound into itself.</para>
///
/// The separate <c>*_delta</c> channels are the genuinely relative ones and compose on top
/// of the held pose. ⚠ **They are unreachable today**: the compiled form ships all 26 of
/// them (15 translate, 6 rotate, 5 scale) as a bare <c>{x,y,z}</c> vector, not as the
/// <c>{from,to}</c> pair <c>Channel</c> looks for, so every one parses to (null, null) and
/// is dropped; the reader front-end emits no delta channel at all. Fixing that is its own
/// change with its own regression — see `backlog.md`.
///
/// Rotations arrive here as RADIANS from both front-ends. The COMPILED data is radians
/// natively (its extremes settle it: maximum 15.708 = 5π, 99.93% of values ≤ 2π, 228 on
/// exact π/2 multiples — DegToRad-ing those made every rotation ~57× too small). The READER
/// sources are degrees (1,388 of 1,428 nonzero values exceed 2π, max 900) and
/// <c>AnimDefs</c> converts them at parse — the same reader↔compiled unit divergence as
/// XYZ_ROTATION. Unconverted they spun C2's roadblock cars ~9 turns through a 35° swerve.
///
/// A missing FROM means "from where the node already is" — the held component, which is
/// what <c>AnimDefs.AddFromTo</c> has always documented as the intent — or "from no offset"
/// for the delta channels. Every absolute channel in the compiled data ships both ends
/// (919/919 rotate, 401/401 translate, 663/663 scale), so this only bites the reader path.
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

    private Vector3? _tdFrom, _tdTo, _rdFrom, _rdTo, _sdFrom, _sdTo;

    private float _t, _runTime;

    public Node3D Target { get; private init; } = null!;

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _t >= _runTime;

    private bool HasAnyChannel =>
        _tTo != null || _rTo != null || _sTo != null ||
        _tdTo != null || _rdTo != null || _sdTo != null;

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
        (m._tdFrom, m._tdTo) = Channel(data, "translate_delta");
        (m._rdFrom, m._rdTo) = Channel(data, "rotate_delta");
        (m._sdFrom, m._sdTo) = Channel(data, "scale_delta");
        return m.HasAnyChannel ? m : null;
    }

    public void Tick(float dt) => Seek(_t + dt);

    public void Seek(float t)
    {
        _t = t;
        float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);

        // Each channel either drives its component or leaves the held value alone; the
        // deltas then compose on top of whichever of the two won.
        var origin = _heldOrigin;
        if (_tTo is { } tTo)
            origin = (_tFrom ?? _heldOrigin).Lerp(tTo, u);
        if (_tdTo is { } tdTo)
            origin += (_tdFrom ?? Vector3.Zero).Lerp(tdTo, u);

        var rot = _heldRot;
        if (_rTo is { } rTo)
            rot = Euler((_rFrom ?? _heldEuler).Lerp(rTo, u));
        if (_rdTo is { } rdTo)
            rot *= Euler((_rdFrom ?? Vector3.Zero).Lerp(rdTo, u));

        var scale = _heldScale;
        if (_sTo is { } sTo)
            scale = (_sFrom ?? _heldScale).Lerp(sTo, u);
        if (_sdTo is { } sdTo)
            scale *= (_sdFrom ?? Vector3.One).Lerp(sdTo, u);

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
