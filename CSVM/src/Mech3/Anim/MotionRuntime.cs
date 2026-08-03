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
/// <item><c>translation_range</c> is a launch in SPHERICAL form, not a distance: <c>xz</c> is an
///   AZIMUTH and <c>y</c> an ELEVATION, both in DEGREES, and <c>initial</c> is the launch SPEED
///   in m/s (<c>delta</c> a speed ramp over the run time). Measured over all 1,217 events
///   install-wide (<c>analysis/object-motion-range/</c>): every <c>xz</c> lies in [−170, 359];
///   every <c>y</c> but one lies in [−90, 90] and goes negative exactly where the thing falls
///   (a balloon turret's parts at −70…−90, a helium tank blowing sideways at 1…2); and the
///   five <c>fly_trailN</c> of one explosion carry evenly spaced <c>xz</c> bands — 35–55,
///   85–105, 135–165, 185–205, 235–255 — i.e. a starburst around the circle. Read as distances
///   those became a quarter-kilometre sideways throw, which is what put the trails far from
///   their explosion and made a fan read as scatter.
///   ⚠ Which world bearing azimuth 0 points along (+X here) is a choice, not a decode — the
///   data fixes the trails' spacing relative to each other, not their absolute compass.</item>
/// <item><c>gravity.value</c> (negative) accelerates the launch; folded straight into the
///   constant acceleration. It is an ABSOLUTE m/s², not an offset to the aircraft's arcade
///   <c>nom_gravity</c> of 20: the census carries a literal <b>−9.8</b> on 173 events (and −10
///   on 400), which is Earth gravity spelled out. The weak values (−1/−2/−3) sit on smoke
///   trails, where floating is the authored look. <c>do_intersections</c> ground-rest and the
///   <c>bounce_sequence</c> re-launch are a Layer-1.5 follow-up (they need a physics ray) — the
///   body integrates freely over the run time and then finishes.</item>
/// <item><c>forward_rotation.Time.initial</c> is a tumble RATE (rad/s) about the node's local
///   X axis (a piece = 15.708 = 900°/s). ⚠ the axis is a reasoned choice — the data carries a
///   scalar rate, not an axis — an end-over-end tumble about the local X reads well for
///   scattered wreckage.</item>
/// <item><c>xyz_rotation.initial</c> a steady multi-axis spin (rad/s), composed like
///   <see cref="SpinMotion"/>; present only on the rare spin+ballistic events.</item>
/// <item><c>scale.initial</c> and <c>scale.delta</c> are OFFSETS from unit scale, not absolute
///   sizes: <c>scale = 1 + initial + delta·u</c>. Unlike <c>PoseScale</c>/<c>OBJECT_SCALE_STATE</c>,
///   which are absolute. Settled by the install's commonest value — a bare
///   <c>(-0.1, -0.1, -0.1)</c> with zero delta, on <b>30 of the 45 distinct SCALE events</b>
///   (every `h2twr`/`radiotwr`/`transmitter` collapse and every `gullfly`): as an absolute that is
///   a NEGATIVE scale, i.e. the object inside-out at a tenth of its size; as an offset it is a
///   clean 10 % shrink, which is what the water tower collapsing actually does (user playtest).
///   ⚠ The base is <c>Vector3.One</c>, and whether it should instead be the node's own authored
///   scale is UNDECIDED — every node carrying this channel is authored at exactly unit scale in
///   this install, so the two readings coincide and no capture can separate them.</item>
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

    /// <summary>The flight time this body actually runs for: the authored <c>RUN_TIME</c>, or —
    /// for a bounce-terminated launch, which carries none — the time its own parabola takes to
    /// return to launch height. The caller reads it back rather than trusting the authored value,
    /// since only <see cref="Create"/> knows the randomised launch the solve rests on.</summary>
    public float RunTime => _runTime;

    /// <summary>The ON_CALL sequence this body owes when it lands — <c>BOUNCE_SEQUENCE</c>'s
    /// <c>default</c> branch — or null when nothing is owed. Armed only on a launch whose flight
    /// time this class SOLVED, i.e. one the data terminated with a bounce rather than a
    /// <c>RUN_TIME</c>. The 204 events that carry both an authored run time and a bounce are left
    /// alone: 102 of them name a live <c>water</c> branch (<c>p1grndhit</c> vs its wet twin), and
    /// choosing between the branches needs the struck collider that <c>BL-245</c> will cast for.
    /// </summary>
    public string? PendingBounce { get; private set; }

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

        // The velocity ramp is a change spread OVER the run time, so it cannot be divided out
        // until the run time is known — and a bounce-terminated launch does not know its own
        // until the parabola is solved below. Collected here, folded in after that.
        var rampTotal = Vector3.Zero;

        if (data.Obj("translation") is { } tr)
        {
            var v0 = tr.Vec3("initial");
            var rnd = tr.Vec3("rnd_xz");
            v0 += new Vector3(RandSym() * rnd.X, RandSym() * rnd.Y, RandSym() * rnd.Z);
            // delta ramps velocity over run_time → a constant acceleration of delta/run_time.
            rampTotal = tr.Vec3("delta");
            m._v0 = v0 + InheritedLocal();
            m._accel = new Vector3(0f, gravity, 0f);
            m._hasBallistic = true;
        }
        else if (data.Obj("translation_range") is { } range)
        {
            // A launch in SPHERICAL form: `xz` is an azimuth and `y` an elevation, both in
            // DEGREES, and `initial` is the launch speed in m/s (`delta` a speed ramp over the
            // run time). Not a distance travelled — see the class remark for the census.
            // `translation_range_min_only` marks the rows whose `max` fields are all 0 and
            // meaningless; there the min IS the value, and interpolating toward 0 would aim
            // every one of them at a bearing and elevation the data never asked for.
            bool minOnly = data.Bool("translation_range_min_only");
            float Pick(AnimData? o)
            {
                if (o == null)
                    return 0f;
                float lo = o.Num("min") ?? 0f;
                return minOnly ? lo : Rand(lo, o.Num("max") ?? 0f);
            }
            float azimuth = Pick(range.Obj("xz"));
            float elevation = Pick(range.Obj("y"));
            float speed = Pick(range.Obj("initial"));
            float speedRamp = Pick(range.Obj("delta"));
            var dir = RangeLaunchDirection(azimuth, elevation);
            m._v0 = dir * speed + InheritedLocal();
            // delta ramps the launch speed over run_time, along the same direction — the same
            // shape `translation.delta` has, and 0 on 984 of the 1,217 events.
            rampTotal = dir * speedRamp;
            m._accel = new Vector3(0f, gravity, 0f);
            m._hasBallistic = true;
        }

        if (data.Obj("scale") is { } sc)
        {
            m._scaleInit = sc.Vec3("initial");
            m._scaleDelta = sc.Vec3("delta");
            m._hasScale = m._scaleInit.LengthSquared() > 1e-9f;
        }

        // A bounce-terminated launch: the data's idiom for "fly until you hit something" omits
        // RUN_TIME entirely and names a BOUNCE_SEQUENCE for the landing instead, so there is no
        // authored duration to run for and the old `run_time ?? 0` read it as "duration 0" —
        // posing 150 reachable pieces at rest on the wreck they should have left (BL-240).
        //
        // ⚠ Ending it when the parabola returns to LAUNCH HEIGHT is a CHOICE, not a decode. The
        // original tested real ground through `do_intersections`; this agrees wherever the ground
        // under the object is flat, which is every reachable case here — debris thrown off a
        // ground-sitting structure. A down-ray replaces it, and must, for the 379 events that
        // FALL rather than launch (a shot-down zeppelin, a parachutist): those have no apex, this
        // solve declines them, and they stay posed at rest until BL-245 lands the ray.
        //
        // Solved in the node's PARENT frame, the same frame the launch and gravity already live
        // in. Absent-RUN_TIME is read from the data, not from `runTime <= 0`, so an authored 0
        // keeps meaning zero.
        if (m._hasBallistic && data.Num("run_time") is null && data.Has("bounce_sequence"))
        {
            float flight = FlightToLaunchHeight(m._v0.Y, m._accel.Y);
            if (flight > 0f)
            {
                rtSafe = flight;
                m._runTime = flight;
                // Landing is the only thing that ends this body, so the sequence the data names
                // for the landing rides with it: every one of these 150 carries `default` alone.
                m.PendingBounce = data.Obj("bounce_sequence")?.Str("default");
            }
        }

        // Everything denominated in the run time, folded in once it is settled — including the
        // solved flight above, or a bounce-terminated piece would fly without its authored tumble.
        m._accel += rtSafe > 0f ? rampTotal / rtSafe : Vector3.Zero;

        // forward_rotation.Time.initial is a TOTAL angle over run_time (the `Time`
        // parameterization), not a rate: the crash pieces carry 5π and 4.44π (clean multiples of
        // π), which read as a rate spin at ~15 rad/s (900°/s) — "spins like crazy" (user
        // playtest). ÷ run_time gives 5π over 6 s = 2.5 tumbles, the reference debris tumble.
        float fwdTotal = data.Obj("forward_rotation")?.Obj("Time")?.Num("initial") ?? 0f;
        m._tumbleRate = rtSafe > 0f ? fwdTotal / rtSafe : 0f;
        m._spinRate = data.Obj("xyz_rotation")?.Vec3("initial") ?? Vector3.Zero;

        // A ballistic launch starts from the node's AUTHORED rest pose, not from wherever the last
        // launch left it. The original instances a fresh copy of an effect template per call; we
        // relocate a POOLED copy (PlaceTemplateAt — one per slot since BL-225, still finite), and
        // its children — `fly_trailN` and friends — are never re-homed, so seeding from the live
        // pose made every repeat explosion on that copy start its trails further from the blast
        // than the one before. The two readings agree everywhere a
        // launch is re-homed by something else (the crash's CrashRestPoses, ResetDestructible), so
        // this only changes the case nothing was resetting.
        if (m._hasBallistic)
        {
            m._heldOrigin = rest.Origin;
            m._heldRot = rest.Basis.Orthonormalized();
        }

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

        // scale = 1 + initial + delta·u: the channel is an OFFSET from unit scale, not an absolute
        // scale (see the class remark). Read as absolute, the install's commonest value — a bare
        // (-0.1, -0.1, -0.1) on 30 of the 45 distinct SCALE events — is a NEGATIVE scale.
        var scale = _hasScale ? Vector3.One + _scaleInit + _scaleDelta * u : _heldScale;

        Target.Transform = new Transform3D(basis.Scaled(scale), origin);
    }

    /// <summary>The unit launch direction one <c>translation_range</c> draw asks for, from its
    /// azimuth and elevation in degrees. The ONE expression of that decode: the gun-casing
    /// ejection in <c>ProjectilePool</c> reads the very same <c>gunshell</c> event and used to
    /// spell the maths out for itself, which is how the two came to disagree (INSTR-3).</summary>
    internal static Vector3 RangeLaunchDirection(float azimuthDeg, float elevationDeg)
    {
        float az = Mathf.DegToRad(azimuthDeg);
        float el = Mathf.DegToRad(elevationDeg);
        float cosEl = Mathf.Cos(el);
        return new Vector3(cosEl * Mathf.Cos(az), Mathf.Sin(el), cosEl * Mathf.Sin(az));
    }

    /// <summary>Time for a launch to come back down to the height it left from:
    /// <c>t = 2·v0y / -ay</c>, the non-zero root of <c>v0y·t + ½·ay·t² = 0</c>. Returns 0 for
    /// anything with no apex — launched level or downward, or with no gravity to bring it back
    /// (a <c>chuteman</c>'s constant descent) — which is the caller's signal to leave the body
    /// alone rather than invent a landing.</summary>
    private static float FlightToLaunchHeight(float v0y, float ay)
    {
        if (v0y <= 0f || ay >= 0f)
            return 0f;
        float t = 2f * v0y / -ay;
        return float.IsFinite(t) ? t : 0f;
    }
}
