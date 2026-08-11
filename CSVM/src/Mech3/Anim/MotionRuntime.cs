using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// The full OBJECT_MOTION rigid body: a ballistic translate/launch, a scale ramp and a
/// tumble, driving one node over its run time, in the node's own parent frame (the same
/// absolute-in-parent-frame convention as <see cref="FromToMotion"/>). Each channel is
/// optional and an absent one holds the node's live value, seeded once at creation so the
/// motion cannot compound into itself. This is the reachable half of OBJECT_MOTION — thrown by a
/// crash or a weapon hit; nothing ambient fires it.
///
/// <para><b>Semantics</b> (established from <c>player_crash_dirt</c>'s pieces,
/// <c>call_crash_trails</c> and <c>flydirt</c>; ⚠ several are TUNE, not a settled decode):
/// <list type="bullet">
/// <item><c>translation.initial</c> is the launch VELOCITY (a piece leaves at y=10 m/s);
///   <c>rnd_xz</c> a per-axis random spread added to it (through the runtime's seedable
///   <c>_rng</c>, so a lab replay is deterministic); <c>delta</c> a velocity ramp over the
///   run time (change from initial to initial+delta) — 0 on every reachable piece, so its
///   exact reading is near-invisible.</item>
/// <item><c>translation_range</c> is a launch in POLAR form, not a distance: <c>xz</c> is an
///   AZIMUTH and <c>y</c> an ELEVATION, both in DEGREES, and <c>initial</c> is the launch SPEED
///   in m/s (<c>delta</c> a speed ramp over the run time). ⚠ The elevation is LINEAR, not
///   spherical — <c>dirY = elev/90</c> with the horizontal taking the L1 remainder
///   <c>1 − |elev|/90</c>, so the direction is NOT unit length (0.707 at 45°) and only the azimuth
///   goes through a sincos. Transcribed from <c>FUN_004e8fa0</c>; see
///   <see cref="RangeLaunchDirection"/>, which is where the whole of it lives. Measured over all 1,217 events
///   install-wide (<c>analysis/object-motion-range/</c>): every <c>xz</c> lies in [−170, 359];
///   every <c>y</c> but one lies in [−90, 90] and goes negative exactly where the thing falls
///   (a balloon turret's parts at −70…−90, a helium tank blowing sideways at 1…2); and the
///   five <c>fly_trailN</c> of one explosion carry evenly spaced <c>xz</c> bands — 35–55,
///   85–105, 135–165, 185–205, 235–255 — i.e. a starburst around the circle. Read as distances
///   those became a quarter-kilometre sideways throw, which is what put the trails far from
///   their explosion and made a fan read as scatter.
///   ⚠ Which world bearing azimuth 0 points along (+X here) is a choice, not a decode — the
///   data fixes the trails' spacing relative to each other, not their absolute compass.</item>
/// <item><c>gravity.value</c> (negative) accelerates the launch; folded into the constant
///   acceleration. It is an ABSOLUTE m/s², not an offset to the aircraft's arcade
///   <c>nom_gravity</c> of 20: the census carries a literal <b>−9.8</b> on 173 events (and −10
///   on 400), which is Earth gravity spelled out. The weak values (−1/−2/−3) sit on smoke
///   trails, where floating is the authored look.
///   <para><c>gravity.complex</c> picks which of TWO forms that fold takes. The plain form drops
///   the value into the parent frame's Y, which is right only while that frame is world-aligned;
///   <c>complex</c> takes gravity as a WORLD-down vector and converts it into the frame, so a body
///   under a banked, pitched or inverted parent falls down the WORLD rather than down its own hull.
///   The install authors it on aircraft wreckage alone — 254 events / 25 shapes, every one of them
///   a body whose parent frame carries whatever attitude the aircraft died in — and the two forms
///   agree exactly anywhere else, which is why nothing else needs it. All 254 author a
///   <c>RUN_TIME</c>, so the converted acceleration never reaches
///   <see cref="FlightToLaunchHeight"/>'s solve.</para>
///   Still unresolved: the
///   379 events that FALL with <c>do_intersections: false</c> — no apex to solve, and no authored
///   collider test to tell us where they land — which the body still integrates freely over the
///   run time and then holds at rest.
///   ⚠ For every event that LAUNCHES upward with no authored <c>RUN_TIME</c> — the 152 (150
///   reachable) that name a bounce, plus the 167 that name neither a bounce nor a run time
///   and end with the piece's own deactivation instead — <see cref="FlightToLaunchHeight"/> ends
///   the flight when the parabola returns to launch height: a CHOICE, not a decode. It stands, and
///   deliberately: all 120 of the bounce shape and all 167 of the vanish shape author
///   <c>do_intersections: false</c>, so the original was not collision-testing them either, and the
///   original was confirmed at the controls to sink its debris through terrain the same way.
///   The choice agrees wherever the ground under the piece is flat, which is every reachable case
///   measured. The apex is the ADMISSION TEST, not the bounce: anything with no apex is declined
///   here and left posed at rest.
///   <para>Where the flag IS authored — 166 events, 150 of them RUN_TIME+bounce —
///   <see cref="TryContact"/> sweeps the trajectory against real colliders and ends the body
///   there, which is the original's own test rather than an approximation of it. See
///   <c>docs/formats/destructibles.md</c>'s "Debris tumbles" bullet for the split.</para></item>
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
///
/// <para>⚠ <b>Every speed above is the AUTHORED one; what flies is that scaled by
/// <see cref="DebrisTune"/>.</b> <c>LaunchScale</c> ships at <b>0.65</b> — a judged look matched at
/// the controls against the original, not a decode —
/// so a piece whose data says it leaves at 10 m/s actually leaves at 6.5. Anything computing an
/// expected arc from the extracted numbers must apply the scale, or pin the raw arc with
/// <see cref="DebrisTune.UseAuthored"/> (what the launch suites do) to assert the decode instead.</para>
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

    // ---- ground contact: the `do_intersections` sweep -------------------------------------------
    // Armed only for the 166 events install-wide that author `do_intersections: true` (150 of them
    // RUN_TIME+bounce), and only in a session that handed the runtime a collision mask. Everything
    // else keeps its flag-free behaviour byte-for-byte — the 120 bounce-shape launches and the 167
    // vanish-shape all author `false`, and the original was confirmed at the controls to sink those
    // through the terrain too.
    private bool _contactTest;
    private uint _contactMask;
    private AnimData? _bounce;      // the BOUNCE_SEQUENCE block, chosen from AT CONTACT
    private Func<GodotObject?, bool>? _surfaceIsWater;
    private bool _landed;
    private Vector3 _landedOrigin;  // parent frame — the pose the body holds from contact onward
    private float _landedAt;        // the clock at contact; rotation and scale freeze there too

    public Node3D Target { get; private init; } = null!;

    public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

    public bool Finished => _landed || _t >= _runTime;

    /// <summary>Whether this body carries the original's own collider test — `do_intersections`,
    /// with a session that wired a mask for it. False leaves every pre-existing behaviour alone.</summary>
    public bool TestsContact => _contactTest;

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

    /// <summary>The ON_CALL sequence this body owes when it lands — <c>BOUNCE_SEQUENCE</c>'s
    /// <c>default</c> branch — or null when nothing is owed. Armed only on a launch whose flight
    /// time this class SOLVED <b>and</b> which names a bounce; the bounce-less shape solves the
    /// same way but names none, so it owes nothing and simply advances its sequence when the
    /// flight ends.
    /// <para>The 204 events carrying both an authored run time and a bounce arm somewhere else
    /// entirely — at CONTACT (<see cref="TryContact"/>), since 150 of them author
    /// <c>do_intersections: true</c> and their branch is chosen from the surface they strike. Their
    /// run time is a ceiling, not a duration.</para>
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
        };

        float RandSym() => (float)(rt._rng.NextDouble() * 2.0 - 1.0); // [-1, 1] via the seedable RNG
        float Rand(float a, float b) => a + (float)rt._rng.NextDouble() * (b - a);

        // The debris arc knobs. Both default to 1 and multiply exactly, so an untuned run — and
        // every golden — is byte-identical to a build without them. See DebrisTune for why the
        // launch and the gravity are separate sliders rather than one "size" scalar.
        var gravityBlock = data.Obj("gravity");
        float gravity = (gravityBlock?.Num("value") ?? 0f) * DebrisTune.GravityScale;
        float launchScale = DebrisTune.LaunchScale;

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

        // `do_intersections` — the original's OWN collider test, and the one field that says which
        // bodies it tested. A session that wires no mask (every lab, every headless suite that does
        // not ask for it, and 9 of the 13 golden captures — freecam/viewer/empty-stage, none of
        // which build world colliders) leaves this false and takes the untouched path: that is the
        // fallback, made structural rather than remembered. The remaining 4 goldens (Fly mode) DO
        // wire a mask, but none currently completes a landing inside its own capture window —
        // see analysis/object-motion-goldens/FINDINGS.md (A1). The BOUNCE_SEQUENCE block rides along
        // because a contact-terminated body picks its branch from the SURFACE IT STRUCK, which is
        // not knowable here.
        m._contactMask = rt.ContactMask;
        m._contactTest = (gravityBlock?.Bool("do_intersections") ?? false) && rt.ContactMask != 0;
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
            // Scaled with the launch it ramps, or the tune would bend the arc's shape as well
            // as its size.
            rampTotal = tr.Vec3("delta") * launchScale;
            // InheritedLocal is OUTSIDE the scale: it is the plane's measured momentum, not part
            // of the authored launch, and its magnitude is tuned separately (WreckMomentum).
            m._v0 = (v0 * launchScale) + InheritedLocal();
            m._accel = GravityAccel();
            m._hasBallistic = true;
        }
        else if (data.Obj("translation_range") is { } range)
        {
            // A launch in POLAR form: `xz` is an azimuth and `y` an elevation, both in
            // DEGREES, and `initial` is the launch speed in m/s (`delta` a speed ramp over the
            // run time). Not a distance travelled — see the class remark for the census. The
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
            // As in the vector branch: the authored launch scales, the inherited momentum does not.
            m._v0 = (dir * speed * launchScale) + InheritedLocal();
            // delta ramps the launch speed over run_time, along the same direction — the same
            // shape `translation.delta` has, and 0 on 984 of the 1,217 events.
            rampTotal = dir * speedRamp * launchScale;
            m._accel = GravityAccel();
            m._hasBallistic = true;
        }

        if (data.Obj("scale") is { } sc)
        {
            m._scaleInit = sc.Vec3("initial");
            m._scaleDelta = sc.Vec3("delta");
            m._hasScale = m._scaleInit.LengthSquared() > 1e-9f;
        }

        // A launch with no authored RUN_TIME: the data's idiom for "fly until you hit something"
        // omits the duration and lets the landing end the flight, so there is nothing to run for —
        // a `run_time ?? 0` read poses the pieces at rest on the wreck they should have left.
        //
        // Two shapes reach here, and the gate is the ABSENT run time, not what terminates the
        // flight (census: `analysis/bl-257-nulled-launch/`):
        //   • 120 events name a BOUNCE_SEQUENCE for the landing — the shape that arms
        //     `PendingBounce` below.
        //   • 167 events (119 distinct defs) name NEITHER field and instead follow the launch with
        //     the piece's OWN null-start deactivation, i.e. "fly, then vanish" — the zeppelin
        //     cannon's eight parts, the crane/sign/generator/shack debris. Gating on a bounce
        //     instead reports all 167 as duration 0, so the deactivation lands on the launch tick
        //     and every piece is hidden before it moves (`dblcannon_flying_parts` the repro).
        //
        // ⚠ Ending it when the parabola returns to LAUNCH HEIGHT is a CHOICE, not a decode. The
        // original tested real geometry through `do_intersections` (probably a collider
        // intersection rather than a terrain ray) — false on all 167 of the second
        // shape, and on the whole first one; this agrees wherever the ground under the object is
        // flat, which is every reachable case here — debris thrown off a ground-sitting structure.
        // A down-ray replaces it, and must, for the events that FALL rather than launch (a
        // shot-down zeppelin, a parachutist): those have no apex, `FlightToLaunchHeight` declines
        // them, and they stay posed at rest until a ray lands them. That guard is what keeps
        // this off the falls — it is the apex, not the bounce, that admits a launch,
        // and the census found 8 of the 167 with no reliable apex (a `bridge_truck` dropped level,
        // `susp_bridge`'s burning ropes at −0.5 m/s ± 1, two `fuelbox` rockerarms whose speed range
        // is −45…45) that keep behaving exactly as they do today.
        //
        // Solved in the node's PARENT frame, the same frame the launch and gravity already live
        // in. Absent-RUN_TIME is read from the data, not from `runTime <= 0`, so an authored 0
        // keeps meaning zero.
        if (m._hasBallistic && data.Num("run_time") is null)
        {
            float flight = FlightToLaunchHeight(m._v0.Y, m._accel.Y);
            if (flight > 0f)
            {
                rtSafe = flight;
                m._runTime = flight;
                // Landing is the only thing that ends this body, so the sequence the data names
                // for the landing rides with it: every one of the 150 reachable bounce launches
                // carries `default` alone. Null for the bounce-less shape — the flight ends,
                // the sequence advances, and the piece's own deactivation is what runs next.
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

        // ⚠ A settle hop INHERITS the test from the landing it continues, whatever its own flag
        // says — the one place `do_intersections: false` is read as "unset" rather than
        // "opted out", and it is a judged divergence, not a decode. `player_crash_dirt`'s pieces are
        // the case: `pieceNseq` lands the piece on the ground (flag true), the dispatched `pNhit`
        // throws the SAME node again at +3 m/s over a 5-7 s RUN_TIME with the flag false, and
        // nothing stops it — 3t − 4.9t² is 107 m under the airfield at t=5, seen at the controls as
        // "plane went through ground". These four pieces are 4 of the
        // 16 (def, node) pairs the census calls ground-tested AND left lying there — the only debris
        // a player can walk up to — so burying them defeats the whole point.
        //
        // The narrowness is the whole defence. ⚠ This is NOT a licence to widen to the 379 events
        // that author false (the original sinks those, confirmed at the controls): the
        // mark is set only by a CONTACT landing, is one-shot, and is consumed above, so the only
        // motions it can reach are follow-ups the sweep itself dispatched on a node the data DID
        // flag. A compiled gravity block always carries all four bits, so "false" here cannot be
        // told from "not re-stated" — and the object, not the event, is what the original tests.
        m._contactTest |= continuesLanding && rt.ContactMask != 0;

        // Nothing to drive → no motion (a bare gravity/bounce stub, handled by the caller).
        bool any = m._hasBallistic || m._hasScale || m._tumbleRate != 0f || !m._spinRate.IsZeroApprox();
        return any ? m : null;
    }

    /// <summary>Advances the body one frame — and, for a <c>do_intersections</c> body, asks the
    /// world whether the step it is about to take runs into anything.
    ///
    /// <para>⚠ The sweep lives HERE and never in <see cref="Seek"/>. <c>Seek</c> is also the
    /// pose/scrub entry point — <c>RESET_STATE</c> poses through it (<c>AnimRuntime</c>'s
    /// <c>Seek(0f)</c> calls) and AnimLab's timeline scrubs through it, in both directions — so a
    /// contact test there would fire on a backwards drag and land a piece that never flew.</para></summary>
    public void Tick(float dt)
    {
        if (dt > 0f && TryContact(dt))
            return;
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
            origin = _landed ? _landedOrigin : _heldOrigin + _v0 * tb + 0.5f * tb * tb * _accel;

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
        if (!_contactTest || _landed || !_hasBallistic)
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
        _landedAt = _t + (next - _t) * fraction;
        _landedOrigin = parent.AffineInverse() * point;
        _landed = true;
        // The landing is the whole meaning of a bounce-terminated flight ending, and for this
        // family the branch could not be chosen until now — the struck body is what picks it.
        // MotionSet.Tick turns this into the dispatched Landing, exactly as it already does for
        // the launches that solve their own flight time; nothing downstream changes.
        PendingBounce ??= ChooseBounce(hit["collider"].As<GodotObject>());
        Seek(_landedAt);
        return true;
    }

    /// <summary>The ballistic origin at time <paramref name="t"/>, in the node's parent frame —
    /// the same closed-form solve <see cref="Seek"/> poses with, factored out because the sweep
    /// needs both ends of the step it is about to take. Never consulted for a landed body.</summary>
    private Vector3 BallisticOrigin(float t) => _heldOrigin + _v0 * t + 0.5f * t * t * _accel;

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
