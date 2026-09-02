using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>Which of the original's two contact mechanisms a body carries. <c>None</c> is the
/// structural fallback a session that wired no collision mask takes.</summary>
internal enum MotionContactTier
{
    None,
    Column,
    Sweep,
}

/// <summary>
/// The full OBJECT_MOTION rigid body: a ballistic translate/launch, a scale ramp and a tumble,
/// driving one node over its run time in the node's own parent frame. Each channel is optional;
/// an absent one holds the node's live value, seeded once at creation. Thrown by a crash or a
/// weapon hit; nothing ambient fires it.
/// Decode: docs/org/objectMotion.md. Authored side: docs/formats/anim-definitions.md. Each
/// channel's own rule sits on the field or method that reads it, not here.
/// ⚠ Every launch speed is the AUTHORED one; no global multiplier stands between the data and the
/// flight. If an arc reads wrong, the answer is a further decode or a filed item, never a scalar.
/// </summary>
internal sealed class MotionRuntime : IAnimMotion
{
    // ⚠ TUNE, not a decode. A launched piece starts inside the wreck it left, so it must clear
    // distance or time before the sweep arms, or it pops the bounce on frame 0.
    // ⚠ Do not drop either bound: see docs/org/objectMotion.md's ArmDistance/ArmSeconds row for
    // why both are kept — ArmSeconds does the real work, ArmDistance stays an unfalsified guard.
    private const float ArmDistance = 2f;

    private const float ArmSeconds = 0.1f;

    // How far down TryGroundColumn looks, chosen past any chapter's vertical extent rather than
    // decoded — see docs/org/objectMotion.md's ColumnDepth row.
    // ⚠ Do not shorten it; that stops a piece over a canyon from being tested at all.
    private const float ColumnDepth = 4096f;

    // The original's own watchdog on a launch that authors no RUN_TIME, per tier.
    // ⚠ A backstop, not a duration. Bodies routinely ending here means contact is broken; fix
    // contact, do not tune these down.
    private const float ColumnWatchdog = 15f;

    private const float SweepWatchdog = 35f;

    // What a contact leaves of the body's speed (0.19999999 in the binary, i.e. 0.2 in float).
    // ⚠ Every component keeps its SIGN; the original reflects nothing. What lifts a body clear of
    // the surface is the pose correction in Land, not this.
    private const float Restitution = 0.2f;

    // The two rest thresholds, asymmetric on purpose and tested per axis, not on the horizontal
    // magnitude. Above either, a contact holds the body half a descending step clear of the
    // surface; below all three it rests exactly on it.
    private const float RestHorizontal = 0.1f, RestVertical = 0.5f;

    // Defensive only: the energy test ends a body in two or three contacts, so this never fires.
    private const int MaxContacts = 8;

    // Held pose, seeded once from the live transform: an absent channel carries it through.
    private Basis _heldRot;      // orthonormal; scale kept out

    private Vector3 _heldScale;

    private Vector3 _heldOrigin;

    // Ballistic: origin(t) = held + v0·t + ½·accel·t²  (accel folds gravity + any velocity ramp).
    private Vector3 _v0, _accel;

    private bool _hasBallistic;

    // Scale ramp: scale(t) = init + delta·u,  u = t/run_time clamped. Both are OFFSETS from unit
    // scale, not absolute sizes; base Vector3.One vs. the node's authored scale is undecided
    // (docs/org/objectMotion.md), since every scaled node in this install is authored at unit.
    private Vector3 _scaleInit, _scaleDelta;

    private bool _hasScale;

    // forward_rotation: a live rate about the launch's own perpendicular. angle(t) = rate·t +
    // ½·accel·t², turned about `_tumbleAxis` — zero for a body whose launch direction is vertical
    // or absent.
    private float _tumbleRate, _tumbleAccel;

    private Vector3 _tumbleAxis;

    private Vector3 _spinRate;   // rad/s per local axis (xyz_rotation)

    private float _t, _runTime;

    // What ENDS the body, which is not what parameterises its channels. `_runTime` is the authored
    // RUN_TIME or, absent one, the parabola's return to launch height, and it still drives the
    // tumble rate, the scale ramp and the duration the sequence waits on. `_ceiling` is the
    // termination alone: the authored RUN_TIME, the original's watchdog, or (with no contact tier
    // to back it) `_runTime` itself. Splitting them is what lets a launch carrying no RUN_TIME keep
    // flying until it lands without also stretching its 5pi tumble over 15 seconds.
    private float _ceiling;

    // The original reuses the RUN_TIME slot as this accumulator when no RUN_TIME is authored, and
    // adds to it only on a step whose contact query ran and found NOTHING. A body falling toward
    // ground it can see never accumulates it at all. Zero limit means no watchdog on this body.
    private float _watchdog, _watchdogLimit;

    // ---- ground contact: the default column, or the `do_intersections` sweep ---------------------
    // Selected once at creation, from the gravity block alone; both tiers stay structurally off in
    // a session that wired no collision mask, which is the whole fallback.
    private MotionContactTier _contactTier;
    private bool _complexGravity;   // widens the column's per-step admission — see TryGroundColumn
    private uint _contactMask;
    private AnimData? _bounce;      // the BOUNCE_SEQUENCE block, chosen from AT CONTACT
    private Func<GodotObject?, bool>? _surfaceIsWater;
    private bool _landed;
    // Whether a SURFACE ended it, as against the watchdog. Both freeze the body the same way, so
    // `_landed` cannot answer the tally's question on its own.
    private bool _landedByContact;
    // A contact that does not end the body re-bases the launch: the corrected pose becomes the new
    // origin, the damped velocity the new v0, and this the new zero of its clock.
    private float _ballisticStart;
    private int _contacts;
    private Vector3 _landedOrigin;  // parent frame — the pose the body holds from contact onward
    private float _landedAt;        // the clock at contact; rotation and scale freeze there too

    public Node3D Target { get; private init; } = null!;

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _landed || _t >= _ceiling;

    /// <summary>Whether this body carries either of the original's contact tiers, in a session that
    /// wired a mask. False is the no-collision-world fallback and nothing else.</summary>
    public bool TestsContact => _contactTier != MotionContactTier.None;

    /// <summary>Which tier, read by <see cref="MotionSet"/> so the two are tallied apart. A pair of
    /// counters is the only way to answer whether the default tier is doing anything, since the
    /// sweep alone can carry a healthy-looking total.</summary>
    public MotionContactTier ContactTier => _contactTier;

    /// <summary>Whether the flight actually ended on a SURFACE rather than running a clock out.
    /// The honest half of the contact/fallback tally: a `--fly` session reporting zero of these
    /// while <see cref="TestsContact"/> bodies launched is the failure mode worth catching.
    /// ⚠ A watchdog end is not one of these. It freezes the body identically but it is the clock
    /// expiring, and counting it here would report a body that found nothing as a landing.</summary>
    public bool LandedByContact => _landedByContact;

    /// <summary>The duration this body REPORTS: the authored <c>RUN_TIME</c>, or, for a launch
    /// that carries none, the time its own parabola takes to return to launch height.
    /// ⚠ This is the SEQUENCE's number, not the body's. What ends the body is
    /// <see cref="Finished"/>'s own ceiling, which can outlast this — see docs/org/objectMotion.md
    /// on the termination model.</summary>
    public float RunTime => _runTime;

    /// <summary>The ON_CALL sequence this body owes when it lands — <c>BOUNCE_SEQUENCE</c>'s
    /// <c>default</c> branch — or null when nothing is owed. Armed only on a launch whose flight
    /// time this class SOLVED and which names a bounce; a shape with a run time arms its bounce at
    /// <see cref="TryContact"/> instead, from the surface it strikes.</summary>
    public string? PendingBounce { get; private set; }

    // Whether this body turns at all: an authored rate AND an axis to turn it about. A vertical
    // launch has the rate and no axis (see TumbleAxis) — arithmetic, not a guard against it.
    private bool Tumbles => (_tumbleRate != 0f || _tumbleAccel != 0f) && _tumbleAxis != Vector3.Zero;

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

        float Rand(float a, float b) => a + (float)rt._rng.NextDouble() * (b - a);

        var gravityBlock = data.Obj("gravity");
        float gravity = gravityBlock?.Num("value") ?? 0f;

        // `complex` (docs/org/objectMotion.md, "Gravity, in two forms") takes gravity as a
        // world-down vector converted into the parent frame, instead of dropped into its Y.
        // ⚠ It does not remove gravity; authored only on aircraft wreckage.
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

        // Tier order matches the original: !DO_INTERSECTIONS, then !NO_ALTITUDE, then the column
        // (docs/org/objectMotion.md, "Contact is the default, in two tiers"). No mask wired
        // selects neither tier — the structural fallback, not a remembered case.
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

        // Is this the `pNhit` settle hop a landing dispatched onto the node that just rested?
        // Asked once here since it gates both the no-inherited-momentum and no-rehome rules below.
        bool continuesLanding = !target.TopLevel
                                && (data.Has("translation") || data.Has("translation_range"))
                                && rt.ConsumeLandingResume(target);

        // IMPACT_FORCE: the owning object's momentum, whole, in the node's parent frame, on the
        // original's own three tests (docs/org/objectMotion.md) rather than a curated name list.
        // ⚠ A settle hop inherits NOTHING; re-inheriting it tunnelled pieces through the ground.
        bool impactForce = data.Bool("impact_force");
        Vector3 InheritedLocal()
        {
            if (!impactForce || continuesLanding || !rt.InheritedVelocityArmed)
                return Vector3.Zero;
            // The original skips a node whose parent count is not exactly 1 (`004e9275`). A detached
            // node is the reachable half of that here; a second parent cannot be represented.
            if (target.GetParent() is not Node3D parent)
                return Vector3.Zero;
            // As GravityAccel: a TopLevel node's own transform IS world, so there is no frame to
            // convert into.
            var parentBasis = target.TopLevel ? Basis.Identity : parent.GlobalTransform.Basis;
            return parentBasis.Inverse() * rt.InheritedWorldVelocity;
        }

        if (data.Obj("translation") is { } tr)
        {
            // `rnd_xz` is the compiled launch DIRECTION, the cache the tumble reads back; the
            // parser built `initial` and `delta` from it and nothing here draws a random number
            // (docs/org/objectMotion.md). ⚠ Read as a spread it walked the Barracuda ±44 m.
            m._tumbleAxis = TumbleAxis(tr.Vec3("rnd_xz"));
            // `delta` folds straight into acceleration, never divided by run_time
            // (docs/org/objectMotion.md). InheritedLocal adds the whole inherited velocity or none.
            m._v0 = tr.Vec3("initial") + InheritedLocal();
            m._accel = GravityAccel() + tr.Vec3("delta");
            m._hasBallistic = true;
        }
        else if (data.Obj("translation_range") is { } range)
        {
            // Polar launch (docs/org/objectMotion.md): xz azimuth, y elevation in degrees,
            // initial the launch speed. `min_only` rows have a meaningless max: there the min IS
            // the value, and interpolating toward it would aim the launch somewhere unauthored.
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
            // The tumble turns about THIS draw's own perpendicular: the original caches the
            // direction at +0x70/0x74/0x78 here and the FORWARD_ROTATION branch reads it back.
            m._tumbleAxis = TumbleAxis(dir);
            m._v0 = (dir * speed) + InheritedLocal();
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

        // No RUN_TIME: "fly until something stops it" (docs/org/objectMotion.md).
        // ⚠ Gate on the absent run time, not on what terminates the flight — a bounce-only gate
        // hides the 167 vanish-shape events on the launch tick (`dblcannon_flying_parts`).
        bool untimed = m._hasBallistic && data.Num("run_time") is null;
        if (untimed)
        {
            float flight = FlightToLaunchHeight(m._v0.Y, m._accel.Y);
            if (flight > 0f)
                m._runTime = flight;
        }

        // Ceiling: RUN_TIME, or the tier's watchdog for an untimed launch (docs/org/objectMotion.md).
        // ⚠ Every untimed ballistic event authors gravity, so this cannot run forever; a future
        // gravity-0 untimed event would need its own cap.
        m._watchdogLimit = untimed
            ? m._contactTier switch
            {
                MotionContactTier.Column => ColumnWatchdog,
                MotionContactTier.Sweep => SweepWatchdog,
                _ => 0f,
            }
            : 0f;
        m._ceiling = m._watchdogLimit > 0f ? float.PositiveInfinity : m._runTime;

        // A bounce normally arms at contact or the watchdog's null-surface `default`. The no-tier
        // fallback never reaches either, so a launch naming a bounce must own it up front.
        if (untimed && m._contactTier == MotionContactTier.None && m._runTime > 0f)
            m.PendingBounce = data.Obj("bounce_sequence")?.Str("default");

        // forward_rotation.Time is a rate and its own acceleration; run_time never enters
        // (docs/org/objectMotion.md). ⚠ The crash pieces' clean multiples of π are coincidence,
        // not evidence for the retired total-angle reading.
        var fwd = data.Obj("forward_rotation")?.Obj("Time");
        m._tumbleRate = fwd?.Num("initial") ?? 0f;
        m._tumbleAccel = fwd?.Num("delta") ?? 0f;
        m._spinRate = data.Obj("xyz_rotation")?.Vec3("initial") ?? Vector3.Zero;

        // ⚠ Do not re-seat a launch on the authored rest pose; that teleported the Barracuda to the
        // map origin between its surfacing FromTo and its drive. The original integrates from the
        // node's live translation and writes no start position (docs/org/objectMotion.md).
        if (rt.DebugMotions && m._hasBallistic)
        {
            var host = target.GetParent() as Node3D;
            Utils.Log.Info("anim", $"anim: launch seed '{target.Name}' live at {m._heldOrigin} (authored {rest.Origin}) under '{host?.Name}' {host?.Transform.Origin} rot {host?.Transform.Basis.GetEuler()} toplevel={target.TopLevel}");
        }

        // Nothing to drive → no motion (a bare gravity/bounce stub, handled by the caller).
        bool any = m._hasBallistic || m._hasScale || m.Tumbles || !m._spinRate.IsZeroApprox();
        return any ? m : null;
    }

    /// <summary>Advances the body one frame, and for a contact-tested body asks the world whether
    /// the step it is about to take ends under something.
    /// ⚠ Both tiers live HERE, never in <see cref="Seek"/>. <c>Seek</c> is also the pose/scrub
    /// entry point in both directions, so a contact test there would land a piece on a backwards
    /// drag that never flew.</summary>
    public void Tick(float dt)
    {
        bool contacted = dt > 0f && _contactTier switch
        {
            MotionContactTier.Column => TryGroundColumn(dt),
            MotionContactTier.Sweep => TryContact(dt),
            _ => false,
        };
        if (contacted)
            return;
        Seek(_t + dt);
    }

    public void Seek(float t)
    {
        _t = t;
        // The clock stops at the CEILING; channels are parameterised by the REPORTED duration.
        // The two differ only on an untimed launch, which keeps flying to its landing while its
        // tumble keeps the rate its own parabola set.
        float tb = _ceiling > 0f ? Mathf.Min(t, _ceiling) : t;
        if (_landed)
            tb = Mathf.Min(tb, _landedAt);
        float u = _runTime <= 0f ? 1f : Mathf.Clamp(tb / _runTime, 0f, 1f);

        var origin = _heldOrigin;
        if (_hasBallistic)
            origin = _landed ? _landedOrigin : BallisticOrigin(tb);

        var basis = _heldRot;
        if (Tumbles)
        {
            // Composes in euler, adding the triple into the node's own angles rather than turning
            // the live basis about an axis, matching the original (docs/org/objectMotion.md).
            var euler = _heldRot.GetEuler(EulerOrder.Yxz)
                        + _tumbleAxis * TumbleAngle(_tumbleRate, _tumbleAccel, tb);
            basis = Basis.FromEuler(euler, EulerOrder.Yxz);
        }

        if (!_spinRate.IsZeroApprox())
        {
            var a = _spinRate * tb;
            if (a.X != 0f) basis = basis.Rotated(basis.X.Normalized(), a.X);
            if (a.Y != 0f) basis = basis.Rotated(basis.Y.Normalized(), a.Y);
            if (a.Z != 0f) basis = basis.Rotated(basis.Z.Normalized(), a.Z);
        }

        // scale = 1 + initial + delta·u — an OFFSET from unit scale, not an absolute one; read as
        // absolute, the install's commonest value is a NEGATIVE scale.
        var scale = _hasScale ? Vector3.One + _scaleInit + _scaleDelta * u : _heldScale;

        Target.Transform = new Transform3D(basis.Scaled(scale), origin);
    }

    /// <summary>The launch direction one <c>translation_range</c> draw asks for, from its azimuth
    /// and elevation in degrees. The ONE expression of that decode: <c>ProjectilePool</c>'s
    /// gun-casing ejection reads the same <c>gunshell</c> event and must share this.
    /// ⚠ Deliberately NOT unit length — see docs/org/objectMotion.md's linear-elevation decode.
    /// Do not normalise it; that is the retired reading, and it launches 60–70° debris 20–25%
    /// too fast. Which world bearing azimuth 0 points along (+X) is a CHOICE, not a decode.</summary>
    internal static Vector3 RangeLaunchDirection(float azimuthDeg, float elevationDeg)
    {
        float az = Mathf.DegToRad(azimuthDeg);
        // 1/90 as the original spells it — a multiply by the literal, not a divide.
        float dirY = elevationDeg * 0.011111111f;
        float horiz = dirY < 0f ? dirY + 1f : 1f - dirY;
        return new Vector3(Mathf.Cos(az) * horiz, dirY, Mathf.Sin(az) * horiz);
    }

    /// <summary>The axis one <c>forward_rotation</c> tumble turns about: the horizontal
    /// perpendicular of the launch direction, drawn by <see cref="RangeLaunchDirection"/> or read
    /// from the vector form's compiled <c>rnd_xz</c> (docs/org/objectMotion.md).
    /// ⚠ Deliberately NOT unit length, same reason as the launch direction: a steep throw tumbles
    /// slowly off the same authored rate. Shared with <c>ProjectilePool</c>'s casing ejection; do
    /// not spell this twice. A zero or vertical direction in means no tumble, by arithmetic.</summary>
    internal static Vector3 TumbleAxis(Vector3 launchDir) => new(launchDir.Z, 0f, -launchDir.X);

    /// <summary>How far a tumble has turned at <paramref name="t"/>: the closed-form integral of a
    /// rate that is itself integrating its own <c>delta</c> each frame.</summary>
    internal static float TumbleAngle(float rate, float accel, float t) => (rate + 0.5f * accel * t) * t;

    // Time for a launch to come back down to the height it left from: the non-zero root of
    // v0y·t + ½·ay·t² = 0. Returns 0 for anything with no apex, which tells the caller to leave
    // the body alone rather than invent a landing.
    private static float FlightToLaunchHeight(float v0y, float ay)
    {
        if (v0y <= 0f || ay >= 0f)
            return 0f;
        float t = 2f * v0y / -ay;
        return float.IsFinite(t) ? t : 0f;
    }

    // The default contact tier: reads the body's own column and ends the flight once the next step
    // would put it under whatever is there (docs/org/objectMotion.md, "Contact is the default").
    // ⚠ Departs from the decode on purpose: a pair of rays instead of a cell-record pick, and
    // `intersect_surface` colliders without the original's `altitude_surface` filter — see that
    // page's divergence table for why. ⚠ No ArmDistance/ArmSeconds epsilon, unlike the sweep: a
    // launch climbs before it falls, so it cannot contact what it left on its first frame.
    private bool TryGroundColumn(float dt)
    {
        if (_landed || !_hasBallistic)
            return false;

        // No collision world means the flag-free behaviour, untouched. Gated on the live space
        // state rather than on SessionSpec, exactly as the sweep is.
        if (Target.GetWorld3D()?.DirectSpaceState is not { } space)
            return false;

        // The solve is in the node's parent frame and the column is a world-space vertical, so both
        // ends of the step go through the parent and the resting pose comes back the same way.
        float next = _ceiling > 0f ? Mathf.Min(_t + dt, _ceiling) : _t + dt;
        var parent = (Target.GetParent() as Node3D)?.GlobalTransform ?? Transform3D.Identity;
        var from = parent * BallisticOrigin(_t);
        var to = parent * BallisticOrigin(next);
        // A descending step admits the test, `complex` widens it to every step. It decides here
        // too, since the column read below can answer with a surface above the body, exactly as
        // the original's cell query does.
        if (!_complexGravity && to.Y >= from.Y)
            return false;

        // The original's column is a query at (x, z), so a body already under a surface is lifted
        // back onto it. ⚠ The second cast comes DOWN from above, never up from below: a one-sided
        // collider answers nothing from behind, and every water polygon in the install is one.
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            from, from + Vector3.Down * ColumnDepth, _contactMask));
        if (hit.Count == 0)
            hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                from + Vector3.Up * ColumnDepth, from, _contactMask));
        if (hit.Count == 0)
            return Watchdog(dt, next);

        float surfaceY = hit["position"].AsVector3().Y;
        if (to.Y >= surfaceY)
            return false;

        // The body keeps the step's horizontal travel and gives up only its descent, as the
        // original does: it rewrites the step's Y component alone and leaves X and Z be.
        return Land(next, parent.AffineInverse() * new Vector3(to.X, surfaceY, to.Z),
            hit["collider"].As<GodotObject>(), byContact: true, worldStepY: to.Y - from.Y);
    }

    // The do_intersections sweep: cast the step the body is about to take, last origin to next in
    // WORLD space, and stop at whatever it meets first. A segment, not a downward ray, because
    // only a segment can rest a piece on a rooftop or stop it against a wall — the agyrobus lost
    // between C5 buildings is the case that chose this.
    private bool TryContact(float dt)
    {
        if (_landed || !_hasBallistic)
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
            return Watchdog(dt, next);

        var point = hit["position"].AsVector3();
        // Where in the step the contact happened, so the tumble and scale freeze at the moment of
        // impact rather than snapping to the end of the frame.
        float span = from.DistanceTo(to);
        float fraction = span > 0f ? Mathf.Clamp(from.DistanceTo(point) / span, 0f, 1f) : 0f;
        return Land(_t + (next - _t) * fraction, parent.AffineInverse() * point,
            hit["collider"].As<GodotObject>(), byContact: true, worldStepY: to.Y - from.Y);
    }

    // The original's watchdog, charged only by a contact query that ran and found nothing — a
    // body descending toward ground it can see never accumulates a tick, so this is not a flight
    // timer (docs/org/objectMotion.md, "The termination model"). Bounds only an untimed launch.
    private bool Watchdog(float dt, float next)
    {
        if (_watchdogLimit <= 0f)
            return false;
        _watchdog += dt;
        return _watchdog >= _watchdogLimit
               && Land(next, BallisticOrigin(next), null, byContact: false);
    }

    // Comes to rest, the shared response both tiers end on: they differ only in what they ask the
    // world, never in what they do with the answer. The bounce branch is chosen here, since the
    // struck body is what picks it; MotionSet.Tick turns this into the dispatched Landing.
    private bool Land(float time, Vector3 parentOrigin, GodotObject? struck, bool byContact,
        float worldStepY = 0f)
    {
        // The velocity this contact arrives with, which decides both halves of the response.
        var incoming = _v0 + _accel * (time - _ballisticStart);
        // Held half a descending step clear of the surface while it is still moving; resting
        // exactly on it once all three thresholds are met. This is the ONLY thing that lifts a
        // body: the velocity below keeps every sign it arrived with.
        bool moving = Mathf.Abs(incoming.X) >= RestHorizontal
                      || Mathf.Abs(incoming.Z) >= RestHorizontal
                      || Mathf.Abs(incoming.Y) >= RestVertical;
        var pose = parentOrigin;
        if (byContact && moving)
            pose += ParentUp() * Mathf.Abs(worldStepY * 0.5f);

        // Energy: the contact is survivable while the incoming speed still covers the acceleration
        // driving it. Each one takes four fifths of the speed, so a piece striking at 20 m/s under
        // Earth gravity damps to 4 and ends on its next contact: one hop or two, never a count.
        if (byContact && incoming.LengthSquared() >= _accel.LengthSquared() && _contacts < MaxContacts)
        {
            _heldOrigin = pose;
            _v0 = incoming * Restitution;
            _ballisticStart = time;
            _contacts++;
            Seek(time);
            return true;
        }

        _landedAt = time;
        _landedOrigin = pose;
        _landed = true;
        _landedByContact = byContact;
        PendingBounce ??= ChooseBounce(struck);
        Seek(_landedAt);
        return true;
    }

    // World up, in the node's parent frame — the direction the pose correction lifts along.
    private Vector3 ParentUp()
    {
        var parent = (Target.GetParent() as Node3D)?.GlobalTransform.Basis;
        return parent is { } b ? b.Inverse() * Vector3.Up : Vector3.Up;
    }

    // The ballistic origin at time t, in the node's parent frame; factored out because the sweep
    // needs both ends of the step it is about to take. Measured from _ballisticStart rather than
    // 0, since a contact the body survives re-bases the launch at the surface.
    private Vector3 BallisticOrigin(float t) => LaunchLaw.Origin(_heldOrigin, _v0, _accel, t - _ballisticStart);

    // The BOUNCE_SEQUENCE branch a contact selects, from the surface it struck. Water is the only
    // distinction drawn, and only where the block authors one; a null `water` branch falls back
    // to `default` rather than suppressing the bounce.
    // ⚠ No `lava` path, and it is not an omission: 0 of the install's BOUNCE_SEQUENCE blocks name
    // one (docs/org/objectMotion.md). A lava path would be untestable dead code.
    private string? ChooseBounce(GodotObject? struck)
    {
        if (_bounce == null)
            return null;
        if (_surfaceIsWater?.Invoke(struck) == true && _bounce.Str("water") is { } wet)
            return wet;
        return _bounce.Str("default");
    }
}
