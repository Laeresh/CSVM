using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

internal enum MotionContactTier
{
    None,
    Column,
    Sweep,
}

/// <summary>
/// The full OBJECT_MOTION rigid body: ballistic translation, scale and tumble channels driving
/// one node in its parent frame. Optional channels preserve the node's live value, and the
/// runtime is reached by crash or weapon-hit effects rather than ambient animation. The original
/// runtime decode and retired readings are documented in docs/org/objectMotion.md.
/// </summary>
internal sealed class MotionRuntime : IAnimMotion
{
    /// <summary>⚠ TUNE, not a decode. A launched piece starts inside the wreck it left and a
    /// falling airframe inside its own hull, so a sweep from the first frame reports a contact at
    /// t=0 and pops the bounce before anything has flown. The body must clear ONE of these before
    /// the sweep arms — distance covers the fast launches (10 m/s clears 2 m in 0.2 s), time covers
    /// the falls, which start at rest and would otherwise sit unarmed inside their own geometry
    /// (a warhawk drops 5 cm in the first 0.1 s). Arming on APEX instead was considered and
    /// rejected: the eleven airframes and <c>agyrobus</c> never rise, so they would never arm.
    ///
    /// <para><b>Measured</b> (C1 airfield dive, `--det`). Both values are
    /// kept, and the A/B says why. With arming disabled outright the four LAUNCHES land at
    /// byte-identical coordinates — so the epsilon suppresses no real contact and buys the launches
    /// nothing here. What it buys is the SETTLE HOP: that body starts lying on the surface, so
    /// unarmed it contacts on its first frame and the authored ~0.5 m hop is cancelled outright
    /// (11 contact endings in the run became 13, all of them hops ending instantly). <c>ArmSeconds</c>
    /// is therefore the limb that does the work and is bounded on both sides — above by the hop's
    /// ~0.6 s round trip, below by one frame. <c>ArmDistance</c> binds only for a body fast enough
    /// to clear 2 m inside 0.1 s (a launch inheriting a dive), and no measured case exercises it;
    /// it stays as an unfalsified guard rather than a confirmed value, and that is the honest
    /// status. ⚠ Do not read the 0.044 s tunnelling that could bury a settle hop as an
    /// argument to tighten this — such a body has no business carrying the dive's momentum at all,
    /// and removing that, not shrinking the window, is the fix that holds (see
    /// <c>InheritedLocal</c>).</para></summary>
    private const float ArmDistance = 2f;

    private const float ArmSeconds = 0.1f;

    private const float GroundColumnRange = 10f;

    private const float ContactDamping = 0.2f;


    // Held pose, seeded once from the live transform: an absent channel carries it through.
    private Basis _heldRot;      // orthonormal; scale kept out

    private Vector3 _heldScale;

    private Vector3 _heldOrigin;

    // Ballistic: origin(t) = held + v0·t + ½·accel·t²  (accel folds gravity + any velocity ramp).
    private Vector3 _v0, _accel;

    private float _ballisticStartTime;

    private bool _hasBallistic;

    // Scale ramp: scale(t) = init + delta·u,  u = t/run_time clamped.
    private Vector3 _scaleInit, _scaleDelta;

    private bool _hasScale;

    private float _tumbleRate;   // rad/s about local X (forward_rotation)

    private Vector3 _spinRate;   // rad/s per local axis (xyz_rotation)

    private float _t, _runTime;
    private float _sequenceDuration;
    private bool _clockBouncePending;

    // ---- ground contact: default column, or the `do_intersections` sweep -------------------------
    // Both stay structurally off when the session wires no collision mask. DO_INTERSECTIONS picks
    // the sweep first; only the default column is vetoed by NO_ALTITUDE.
    private MotionContactTier _contactTier;
    private bool _complexGravity;
    private uint _contactMask;
    private AnimData? _bounce;      // the BOUNCE_SEQUENCE block, chosen from AT CONTACT
    private Func<GodotObject?, bool>? _surfaceIsWater;
    private bool _landed;
    private Vector3 _landedOrigin;  // parent frame — the pose the body holds from contact onward
    private float _landedAt;        // the clock at contact; rotation and scale freeze there too
    private int _reboundCount;

    public Node3D Target { get; private init; } = null!;

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _landed || _t >= _runTime;

    public int ReboundCount => _reboundCount;

    /// <summary>Whether this body uses either original contact tier in a session that wired a mask.
    /// False is the structural no-collision-world fallback.</summary>
    public bool TestsContact => _contactTier != MotionContactTier.None;

    public bool TestsColumnContact => _contactTier == MotionContactTier.Column;

    public bool TestsSweepContact => _contactTier == MotionContactTier.Sweep;

    /// <summary>Whether the flight actually ended on a collider rather than running its clock out.
    /// The honest half of the contact/fallback tally: a `--fly` session reporting zero of these
    /// while <see cref="TestsContact"/> bodies launched is the failure mode worth catching.</summary>
    public bool LandedByContact => _landed;

    /// <summary>The flight time this body actually runs for: the authored <c>RUN_TIME</c>, or —
    /// for a launch that carries none, whether it names a <c>BOUNCE_SEQUENCE</c> or
    /// nothing at all — the time its own parabola takes to return to launch height. The
    /// caller reads it back rather than trusting the authored value, since only
    /// <see cref="Create"/> knows the randomised launch the solve rests on.</summary>
    public float RunTime => _runTime;

    /// <summary>The sequence duration is separate from the internal watchdog. An untimed launch
    /// may use its predicted arc for the following event while the watchdog remains only a backstop.</summary>
    public float SequenceDuration => _sequenceDuration;

    /// <summary>The ON_CALL sequence this body owes when it lands — <c>BOUNCE_SEQUENCE</c>'s
    /// <c>default</c> branch — or null when nothing is owed. Armed only on a launch whose flight
    /// time this class SOLVED <b>and</b> which names a bounce; the bounce-less shape solves the
    /// same way but names none, so it owes nothing and simply advances its sequence when the
    /// flight ends.
    /// <para>A body carrying an authored run time arms its bounce at contact, through either tier,
    /// because the struck surface chooses the branch. Its run time is a ceiling, not a duration.</para>
    /// </summary>
    public string? PendingBounce { get; private set; }

    public static MotionRuntime? Create(AnimRuntime rt, Node3D target, AnimData data, float runTime,
        bool inheritVelocity = true)
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
            _sequenceDuration = rtSafe,
        };

        float RandSym() => (float)(rt._rng.NextDouble() * 2.0 - 1.0); // [-1, 1] via the seedable RNG
        float Rand(float a, float b) => a + (float)rt._rng.NextDouble() * (b - a);

        var gravityBlock = data.Obj("gravity");
        float gravity = gravityBlock?.Num("value") ?? 0f;

        // `complex` is the two-form gravity switch. A motion integrates in its node's PARENT frame,
        // and the plain form drops `gravity.value` straight into that frame's Y — correct only while
        // the frame is world-aligned. `complex` instead takes gravity as a WORLD-down vector and
        // converts it into the frame, so a body under a banked, pitched or inverted parent still
        // falls down the world rather than down its own hull. That is the whole of the difference,
        // and it is why the install authors it on aircraft wreckage alone: 254 events / 25 shapes,
        // the eleven airframes' fall, `player`'s and both `player_crash_*`' pieces, `agyrobus`, the
        // lost rotor and the smoke canister — every body whose parent frame is at whatever attitude
        // the aircraft died in, and nothing else. Under a world-aligned parent the two forms agree
        // exactly, which is why nothing else needs it.
        bool complexGravity = gravityBlock?.Bool("complex") ?? false;
        m._complexGravity = complexGravity;
        Vector3 GravityAccel()
        {
            if (!complexGravity || gravity == 0f)
                return new Vector3(0f, gravity, 0f);
            // A TopLevel node's own transform IS world, so there is no frame to convert into.
            var parentBasis = target.TopLevel
                ? Basis.Identity
                : (target.GetParent() as Node3D)?.GlobalTransform.Basis ?? Basis.Identity;
            return parentBasis.Inverse() * new Vector3(0f, gravity, 0f);
        }

        // DO_INTERSECTIONS selects the geometry sweep outright. Otherwise gravity selects the
        // default column unless NO_ALTITUDE vetoes it. A session that wires no mask leaves both
        // tiers off. The BOUNCE_SEQUENCE rides along because the struck surface chooses it.
        m._contactMask = rt.ContactMask;
        if (gravityBlock != null && rt.ContactMask != 0)
        {
            if (gravityBlock.Bool("do_intersections"))
                m._contactTier = MotionContactTier.Sweep;
            else if (!gravityBlock.Bool("no_altitude"))
                m._contactTier = MotionContactTier.Column;
        }
        m._bounce = data.Obj("bounce_sequence");
        m._surfaceIsWater = rt.SurfaceIsWater;

        // Does this motion CONTINUE a contact landing — i.e. is it the `pNhit` settle hop the
        // sweep itself dispatched onto the very node that just came to rest? One question, three
        // consequences below (no inherited momentum, no re-home to the authored rest, and the
        // contact test carries over), so it is asked once, here, before any of them. The mark is
        // one-shot and consuming it IS the answer. Read `_hasBallistic`'s own condition off the
        // data, since the field is not set until the translation block below.
        bool continuesLanding = !target.TopLevel
                                && (data.Has("translation") || data.Has("translation_range"))
                                && rt.ConsumeLandingResume(target);

        // The plane's momentum (world-space), carried by the launched pieces so they scatter
        // along its travel instead of just popping up in place. Converted into the node's parent
        // frame, where the launch velocity lives (v0 drives Target.Transform, a local pose).
        // `inheritVelocity` opts a caller's node OUT: a ground-planted effect (the crash
        // splash) authors the exact same near-zero-horizontal, vertical-only translation shape as a
        // launched piece, so the data alone cannot tell "debris" from "a decal that must stay put"
        // apart — only the caller (which knows which def this is) can.
        //
        // ⚠ A settle hop inherits NOTHING. The momentum belongs to the aircraft's last moment and
        // the FIRST launch already spent it: the piece is lying on the ground, at rest, and a
        // vertical dive hands its `pNhit` hop ~45 m/s straight down on top of the authored +3.
        // Measured — the hop then covered the 2 m arming epsilon in 0.044 s, i.e. it was already
        // BELOW the terrain when the sweep armed, and every ray after that started underground and
        // found nothing. That is the whole of "the plane went through ground": not a missing test,
        // a body that tunnelled before the test could look.
        Vector3 InheritedLocal()
        {
            if (!inheritVelocity || continuesLanding || rt.InheritedWorldVelocity == Vector3.Zero)
                return Vector3.Zero;
            var parentBasis = (target.GetParent() as Node3D)?.GlobalTransform.Basis ?? Basis.Identity;
            return parentBasis.Inverse() * rt.InheritedWorldVelocity;
        }

        if (data.Obj("translation") is { } tr)
        {
            var v0 = tr.Vec3("initial");
            var rnd = tr.Vec3("rnd_xz");
            v0 += new Vector3(RandSym() * rnd.X, RandSym() * rnd.Y, RandSym() * rnd.Z);
            // `delta` is folded straight into the acceleration slot, no division by run_time —
            // the original stores dir·delta into +0x4c..0x54 and copies it verbatim into the
            // live acceleration.
            m._v0 = v0 + InheritedLocal();
            m._accel = GravityAccel() + tr.Vec3("delta");
            m._hasBallistic = true;
        }
        else if (data.Obj("translation_range") is { } range)
        {
            // A launch in POLAR form: `xz` is an azimuth and `y` an elevation, both in
            // DEGREES, and `initial` is the launch speed in m/s (`delta` a constant acceleration
            // along the same direction, not a speed ramp divided by run_time). Not a distance
            // travelled — see the class remark for the census. The
            // elevation is LINEAR and the direction is not unit length: RangeLaunchDirection owns
            // that, and `speed` here is a scale on a vector shorter than 1 everywhere but 0°/90°.
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
            // `delta` folds in as a constant acceleration along the same direction — the same
            // shape `translation.delta` has, and 0 on 984 of the 1,217 events.
            m._accel = GravityAccel() + dir * speedRamp;
            m._hasBallistic = true;
        }

        if (data.Obj("scale") is { } sc)
        {
            m._scaleInit = sc.Vec3("initial");
            m._scaleDelta = sc.Vec3("delta");
            m._hasScale = m._scaleInit.LengthSquared() > 1e-9f;
        }

        // An absent RUN_TIME uses the original watchdog as a universal backstop. The launch-height
        // solve is retained only for sequence scheduling: it must never determine when the body
        // stops, because contact can end it first and the watchdog must not become a visible
        // 15/35-second sequence hold.
        if (m._hasBallistic && data.Num("run_time") is null)
        {
            m._runTime = m._contactTier == MotionContactTier.Sweep ? 35f : 15f;
            float flight = PredictedFlightDuration(m._v0.Y, m._accel.Y);
            m._sequenceDuration = flight > 0f ? flight : 0f;
            // If no collision world is wired, the watchdog is the only available termination;
            // preserve the authored bounce callback for that fallback. A real contact still
            // chooses its branch in Land before this clock ending is observed.
            m.PendingBounce = data.Obj("bounce_sequence")?.Str("default");
            m._clockBouncePending = m.PendingBounce != null && m._sequenceDuration > 0f;
        }
        // forward_rotation.Time.initial is a TOTAL angle over run_time (the `Time`
        // parameterization), not a rate: the crash pieces carry 5π and 4.44π (clean multiples of
        // π), which read as a rate spin at ~15 rad/s (900°/s) — "spins like crazy" (user
        // playtest). ÷ run_time gives 5π over 6 s = 2.5 tumbles, the reference debris tumble.
        float fwdTotal = data.Obj("forward_rotation")?.Obj("Time")?.Num("initial") ?? 0f;
        m._tumbleRate = rtSafe > 0f ? fwdTotal / rtSafe : 0f;
        m._spinRate = data.Obj("xyz_rotation")?.Vec3("initial") ?? Vector3.Zero;

        // A ballistic launch starts from the node's AUTHORED rest pose, not from wherever the last
        // launch left it. The original instances a fresh copy of an effect template per call; we
        // relocate a POOLED copy (PlaceTemplateAt — one per slot, finite), and
        // its children — `fly_trailN` and friends — are never re-homed, so seeding from the live
        // pose made every repeat explosion on that copy start its trails further from the blast
        // than the one before. The two readings agree everywhere a
        // launch is re-homed by something else (the crash's CrashRestPoses, ResetDestructible), so
        // this only changes the case nothing was resetting.
        //
        // ⚠ EXCEPT a placed template ROOT itself (TopLevel — only the stage's placement write ever
        // sets that): the CALL that started this motion just put it at THIS call's site, and the
        // recorded rest is wherever the FIRST placement froze it — re-basing to it replayed every
        // crash-after-the-first's dirt burst at the first crash's position. The live pose IS the
        // authoritative site for a root; its children keep the authored-rest re-home above.
        //
        // ⚠ AND except a piece continuing from a contact landing: the sequence a landing dispatches
        // re-launches the very node that landed (`pNhit` throws `pieceN` on again with a second,
        // flatter motion), so re-basing teleports it back to the crash point before it flies —
        // seen at the controls as the wreck "jumping back to the crash point" once per piece. The
        // mark is one-shot and consumed here.
        //
        // ⚠ AND except a TAKEOVER: a node another motion is driving RIGHT NOW is somewhere its
        // authored rest knows nothing about, and the launch displaces that motion (MotionSet.Add
        // evicts on the transform channel) rather than replacing a static pose. C5's `agyrobus`
        // is the case — it has no placement of its own at all, so its "authored rest" is the map
        // origin and its whole visible position is `agbus_fly`'s SI-script playback; re-basing
        // there teleported the shot-down bus kilometres away to fall out of sight. Same family as
        // the landing resume above: the live pose is authoritative when something else just put
        // the node there.
        if (m._hasBallistic && !target.TopLevel && !continuesLanding
            && !rt.Motions.DrivesTransform(target))
        {
            m._heldOrigin = rest.Origin;
            m._heldRot = rest.Basis.Orthonormalized();
        }

        // Nothing to drive → no motion (a bare gravity/bounce stub, handled by the caller).
        bool any = m._hasBallistic || m._hasScale || m._tumbleRate != 0f || !m._spinRate.IsZeroApprox();
        return any ? m : null;
    }

    /// <summary>Advances the body one frame and applies its selected contact tier.
    ///
    /// <para>⚠ The sweep lives HERE and never in <see cref="Seek"/>. <c>Seek</c> is also the
    /// pose/scrub entry point — <c>RESET_STATE</c> poses through it (<c>AnimRuntime</c>'s
    /// <c>Seek(0f)</c> calls) and AnimLab's timeline scrubs through it, in both directions — so a
    /// contact test there would fire on a backwards drag and land a piece that never flew.</para></summary>
    public void Tick(float dt)
    {
        if (dt > 0f)
        {
            float next = _runTime > 0f ? Mathf.Min(_t + dt, _runTime) : _t + dt;
            float step = next - _t;
            if (step <= 0f)
                return;
            bool contacted = _contactTier == MotionContactTier.Column
                ? TryGroundColumn(step)
                : TryContact(step);
            if (contacted)
                return;
            Seek(next);
            return;
        }
        Seek(_t + dt);
    }
    public void Seek(float t)
    {
        _t = t;
        // A landed body freezes every channel at the contact moment: the piece is lying on the
        // ground, so it must not keep tumbling or scaling toward its authored end state.
        float tb = _runTime > 0f ? Mathf.Min(t, _runTime) : t;
        if (_landed)
            tb = Mathf.Min(tb, _landedAt);
        float u = _runTime <= 0f ? 1f : Mathf.Clamp(tb / _runTime, 0f, 1f);

        var origin = _heldOrigin;
        if (_hasBallistic)
            origin = _landed ? _landedOrigin : BallisticOrigin(tb);

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

    /// <summary>The launch direction one <c>translation_range</c> draw asks for, from its azimuth
    /// and elevation in degrees. The ONE expression of that decode: the gun-casing ejection in
    /// <c>ProjectilePool</c> reads the very same <c>gunshell</c> event and must share this — two
    /// spellings of the maths is how they disagree.
    ///
    /// <para><b>⚠ This vector is deliberately NOT unit length, and normalising it is the bug.</b>
    /// The elevation is LINEAR, not spherical: the original's <c>TRANSLATION_RANGE</c> block
    /// (<c>FUN_004e8fa0</c>, the <c>flags &amp; 8</c> branch) computes <c>dirY = elev · 0.011111111</c>
    /// — <c>1/90</c>, written out — and gives the horizontal the L1 remainder
    /// <c>1 − |elev|/90</c>, caching the three components at <c>+0x70/+0x74/+0x78</c>. Only the
    /// AZIMUTH is converted <c>deg→rad</c> (<c>· 0.017453292</c>) and passed to the sincos at
    /// <c>FUN_0053c6c0</c>; the elevation never touches a trig call at all. So the length dips to
    /// 0.707 at 45° and returns to 1 at 0° and 90°, and a launch at 60–70° leaves 25–20 % slower
    /// than the unit-sphere reading this replaced — which is what cut <c>m_build03</c> part1's
    /// nine pieces at ~70 % of their authored 5.0 s <c>RUN_TIME</c> instead of landing them
    /// inside it.</para>
    ///
    /// <para>⚠ Which world bearing azimuth 0 points along (+X) stays a CHOICE, untouched by this —
    /// and <c>FUN_0053c6c0</c>'s own output order (which of its two results the original puts on X
    /// and which on Z) has not been checked, so the cos/sin assignment below is inherited, not
    /// decoded.</para></summary>
    internal static Vector3 RangeLaunchDirection(float azimuthDeg, float elevationDeg)
    {
        float az = Mathf.DegToRad(azimuthDeg);
        // 1/90 as the original spells it — a multiply by the literal, not a divide.
        float dirY = elevationDeg * 0.011111111f;
        float horiz = dirY < 0f ? dirY + 1f : 1f - dirY;
        return new Vector3(Mathf.Cos(az) * horiz, dirY, Mathf.Sin(az) * horiz);
    }

    /// <summary>Predicts sequence timing for an untimed upward launch; this is not a termination rule.
    /// Time for a launch to come back down to the height it left from:
    /// <c>t = 2·v0y / -ay</c>, the non-zero root of <c>v0y·t + ½·ay·t² = 0</c>. Returns 0 for
    /// anything with no apex — launched level or downward, or with no gravity to bring it back
    /// (a <c>chuteman</c>'s constant descent) — which is the caller's signal to leave the body
    /// alone rather than invent a landing.</summary>
    internal static float PredictedFlightDuration(float v0y, float ay)
    {
        if (v0y <= 0f || ay >= 0f)
            return 0f;
        float t = 2f * v0y / -ay;
        return float.IsFinite(t) ? t : 0f;
    }

    internal bool ClockBounceReady() => _clockBouncePending && _t >= _sequenceDuration;

    internal string? TakeClockBounce()
    {
        if (!ClockBounceReady())
            return null;
        _clockBouncePending = false;
        string? bounce = PendingBounce;
        PendingBounce = null;
        return bounce;
    }

    /// <summary>The default ground-column tier: inspect the 10 m vertical column ending at the
    /// body's next point. This is deliberately not a trajectory sweep, so intervening walls and
    /// ledges do not become landing surfaces.</summary>
    private bool TryGroundColumn(float dt)
    {
        if (_contactTier != MotionContactTier.Column || _landed || !_hasBallistic)
            return false;

        float next = _runTime > 0f ? Mathf.Min(_t + dt, _runTime) : _t + dt;
        var localFrom = BallisticOrigin(_t);
        var localTo = BallisticOrigin(next);
        // Ordinary bodies consult the column only while descending in their parent frame. COMPLEX
        // bodies consult it every step because world-down was transformed into that frame and its
        // local Y sign no longer says whether the body is falling.
        if (!_complexGravity && localTo.Y - localFrom.Y >= 0f)
            return false;

        if (Target.GetWorld3D()?.DirectSpaceState is not { } space)
            return false;

        var parent = (Target.GetParent() as Node3D)?.GlobalTransform ?? Transform3D.Identity;
        var worldTo = parent * localTo;
        var query = PhysicsRayQueryParameters3D.Create(
            worldTo + Vector3.Up * GroundColumnRange,
            worldTo, _contactMask);
        var excluded = new Godot.Collections.Array<Rid>();
        Godot.Collections.Dictionary hit;
        while (true)
        {
            query.Exclude = excluded;
            hit = space.IntersectRay(query);
            if (hit.Count == 0)
                return false;
            var collider = hit["collider"].As<GodotObject>();
            if (collider?.HasMeta(SceneBuilder.AltitudeSurfaceMeta) == true)
                break;
            if (collider is not CollisionObject3D skipped)
                return false;
            excluded.Add(skipped.GetRid());
        }

        var surface = hit["position"].AsVector3();
        if (worldTo.Y >= surface.Y)
            return false;

        return Land(next, parent.AffineInverse() * new Vector3(worldTo.X, surface.Y, worldTo.Z),
            localTo - localFrom, hit["collider"].As<GodotObject>());
    }

    /// <summary>The <c>do_intersections</c> sweep: cast the step the body is about to take — last
    /// origin to next origin, in WORLD space — and stop the flight at whatever it meets first.
    /// Returns whether contact ended the body.
    ///
    /// <para>A segment along the trajectory, not a ray straight down: the original tested real
    /// geometry, and only a segment can rest a piece on a rooftop or stop it against a wall — the
    /// <c>agyrobus</c> lost between C5 buildings is the case that chose this over a terrain ray.
    /// Masked by whatever the session handed over,
    /// which is the world layer alone: debris is not solid to aircraft, and that follows
    /// <c>CollisionLayers</c>' own rule that world-only probes stay blind to planes.</para></summary>
    private bool TryContact(float dt)
    {
        if (_contactTier != MotionContactTier.Sweep || _landed || !_hasBallistic)
            return false;
        // Not armed yet — see ArmDistance. Judged at the START of the step, so a body arms at
        // worst one frame late rather than testing a segment whose first half is still inside the
        // thing it launched from.
        if (_t < ArmSeconds && (BallisticOrigin(_t) - _heldOrigin).LengthSquared() < ArmDistance * ArmDistance)
            return false;
        // No collision world → the flag-free behaviour, untouched. Gated on the live space
        // state rather than on SessionSpec so this class stays free of the session, and so it
        // self-corrects if the collision rule ever moves.
        if (Target.GetWorld3D()?.DirectSpaceState is not { } space)
            return false;

        // The solve is in the node's PARENT frame; the query is in world space. Convert both ends
        // through the parent, and convert the hit back the same way — getting this backwards yields
        // contacts at plausible-looking but entirely wrong places.
        var parent = (Target.GetParent() as Node3D)?.GlobalTransform ?? Transform3D.Identity;
        float next = _runTime > 0f ? Mathf.Min(_t + dt, _runTime) : _t + dt;
        var from = parent * BallisticOrigin(_t);
        var to = parent * BallisticOrigin(next);
        if (from.DistanceSquaredTo(to) < 1e-8f)
            return false;

        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to, _contactMask));
        if (hit.Count == 0)
            return false;

        var point = hit["position"].AsVector3();
        // Where in the step the contact happened, so the tumble and scale freeze at the moment of
        // impact rather than snapping to the end of the frame.
        float span = from.DistanceTo(to);
        float fraction = span > 0f ? Mathf.Clamp(from.DistanceTo(point) / span, 0f, 1f) : 0f;
        // The landing is the whole meaning of a bounce-terminated flight ending, and for this
        // family the branch could not be chosen until now — the struck body is what picks it.
        // MotionSet.Tick turns this into the dispatched Landing, exactly as it already does for
        // the launches that solve their own flight time; nothing downstream changes.
        return Land(_t + (next - _t) * fraction, parent.AffineInverse() * point,
            parent.Basis.Inverse() * (to - from), hit["collider"].As<GodotObject>());
    }

    private bool Land(float time, Vector3 parentOrigin, Vector3 descendingStep, GodotObject? collider)
    {
        float elapsed = time - _ballisticStartTime;
        var incomingVelocity = _v0 + _accel * elapsed;
        bool moving = new Vector2(incomingVelocity.X, incomingVelocity.Z).Length() >= 0.1f
                      || Mathf.Abs(incomingVelocity.Y) >= 0.5f;
        var contactOrigin = moving ? parentOrigin - descendingStep * 0.5f : parentOrigin;

        // The original reflects only the penetrating STEP, by holding a moving body half that
        // step clear of the surface. Velocity is not reflected: all three components keep their
        // sign at 20%. It continues while incoming speed² still covers acceleration²; otherwise
        // this contact ends the motion at the already-corrected pose.
        if (incomingVelocity.LengthSquared() >= _accel.LengthSquared())
        {
            _heldOrigin = contactOrigin;
            _v0 = incomingVelocity * ContactDamping;
            _ballisticStartTime = time;
            _reboundCount++;
            Seek(time);
            return false;
        }

        _landedAt = time;
        // The half-step offset is only the intermediate contact pose. The final response lands
        // exactly on the struck surface, including when the defensive response ceiling is hit.
        _landedOrigin = parentOrigin;
        _landed = true;
        PendingBounce ??= ChooseBounce(collider);
        Seek(_landedAt);
        return true;
    }

    /// <summary>The ballistic origin at time <paramref name="t"/>, in the node's parent frame —
    /// the same closed-form solve <see cref="Seek"/> poses with, factored out because the sweep
    /// needs both ends of the step it is about to take. Never consulted for a landed body.</summary>
    private Vector3 BallisticOrigin(float t)
    {
        float elapsed = t - _ballisticStartTime;
        return _heldOrigin + _v0 * elapsed + 0.5f * elapsed * elapsed * _accel;
    }

    /// <summary>The <c>BOUNCE_SEQUENCE</c> branch a contact selects, from the surface it struck.
    ///
    /// <para>Water is the only distinction drawn, and only where the block authors one: a null
    /// <c>water</c> branch means the author did not distinguish, so it falls back to
    /// <c>default</c> — it does not mean suppress the bounce. Everything that is not water takes
    /// <c>default</c> too, quicksand included: no bounce sequence names it, and mapping it to water
    /// would invent a splash on sand.</para>
    ///
    /// <para>⚠ No <c>lava</c> path, and it is not an omission: 0 of the install's 324
    /// <c>BOUNCE_SEQUENCE</c> blocks name a lava branch. The field is engine baggage from another
    /// title on the same engine — the only lava in this game is C3's volcano, far too small to
    /// throw debris into. A lava path would be untestable dead code.</para></summary>
    private string? ChooseBounce(GodotObject? struck)
    {
        if (_bounce == null)
            return null;
        if (_surfaceIsWater?.Invoke(struck) == true && _bounce.Str("water") is { } wet)
            return wet;
        return _bounce.Str("default");
    }
}
