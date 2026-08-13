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
///   <c>_rng</c>, so a lab replay is deterministic); <c>delta</c> a constant ACCELERATION added
///   straight into the launch's own (<c>dir·delta</c>, no division by <c>run_time</c>) — 0 on
///   every reachable piece, so its exact reading is near-invisible.</item>
/// <item><c>translation_range</c> is a launch in POLAR form, not a distance: <c>xz</c> is an
///   AZIMUTH and <c>y</c> an ELEVATION, both in DEGREES, and <c>initial</c> is the launch SPEED
///   in m/s (<c>delta</c> a constant acceleration along the same direction, not a speed ramp
///   divided by <c>run_time</c>). ⚠ The elevation is LINEAR, not
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
///   <para>Contact is the DEFAULT, in one of two tiers. Every gravity-bearing ballistic body is
///   tested wherever the session wired a mask: <see cref="TryGroundColumn"/>, a vertical column
///   under the body, unless <c>do_intersections</c> upgrades it to <see cref="TryContact"/>'s
///   trajectory sweep (166 events install-wide). <c>no_altitude</c> is the opt-out and vetoes the
///   column only, and <c>gunshell</c> alone authors it. So the 1,466 default-combination bodies
///   that used to sink through the world now land on it. See
///   <c>docs/formats/destructibles.md</c>'s "Debris tumbles" bullet for the split.</para>
///   ⚠ An event that LAUNCHES upward with no authored <c>RUN_TIME</c> (the 152 that name a bounce,
///   plus the 167 that name neither a bounce nor a run time and end with the piece's own
///   deactivation) still has <see cref="FlightToLaunchHeight"/> end its flight when the parabola
///   returns to launch height. That is a CHOICE, not a decode, and now only the ceiling such a
///   body carries, since either tier ends it earlier on real geometry. The apex is that solve's
///   admission test, not the bounce, so a body with no apex is declined there and simply falls
///   until it lands.</item>
/// <item><c>forward_rotation.Time.initial</c> is a tumble RATE in rad/s (<c>delta</c> its
///   acceleration, non-zero on 2 events install-wide), and the axis is not a mesh axis at all:
///   the original turns the body about the HORIZONTAL PERPENDICULAR of its own launch direction,
///   <c>(dirZ, 0, −dirX)</c>, left unnormalised so its length is the launch's own
///   <c>h = 1 − |elev|/90</c>. A steep throw therefore tumbles slowly and a flat one fast off the
///   same authored number. See <see cref="TumbleAxis"/>.
///   ⚠ A body launched by the VECTOR <c>translation</c> form does not tumble AT ALL: that cache
///   (<c>+0x70/+0x78</c>) is filled only by the <c>translation_range</c> branch, the parser zeroes
///   the whole 0x14c-byte event struct before reading it (<c>REP STOSD</c> at <c>005085e0</c>),
///   and the tumble multiplies straight through it. That is 495 of the install's 1,399 tumbles,
///   the four <c>player_crash_dirt</c> pieces among them. ⚠ The <c>DISTANCE</c>
///   parameterisation (flag <c>0x40</c>, a turn per metre travelled rather than per second) is not
///   built: all 1,399 author <c>Time</c> and none authors <c>Distance</c>.</item>
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
/// <para>Every speed above is the AUTHORED one, and it is what flies. No global multiplier stands
/// between the data and the launch: a piece whose data says it leaves at 10 m/s leaves at 10 m/s.
/// A tuned one used to (0.65, judged at the controls), and it was deleted rather than re-judged
/// once the elevation decode supplied the 0.745–0.81 it was standing in for. If an arc reads wrong
/// from here on, the answer is a further decode or a filed item, never a scalar.</para>
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

    /// <summary>How far down <see cref="TryGroundColumn"/> looks. The original bounds nothing here,
    /// since its query is a grid-cell lookup and the cell's surfaces come back at whatever depth
    /// they sit at. The 10 m in <c>FUN_004e9e30</c> filters candidates above the body, not below
    /// it. A ray needs a finite end, so this is chosen past any chapter's vertical extent (the
    /// highest reachable geometry sits near y=1400) rather than decoded. ⚠ Shortening it would
    /// invent a rule the original does not have, stopping a piece over a canyon from being tested
    /// at all rather than letting it fall to the floor.</summary>
    private const float ColumnDepth = 4096f;

    /// <summary>What bounds a launch that authors no <c>RUN_TIME</c>: the original's own watchdog,
    /// per tier, read off <c>FUN_004e8fa0</c>. ⚠ A backstop, not a duration. Bodies routinely
    /// ending here means contact is broken, and the answer is to fix contact rather than to tune
    /// these down.</summary>
    private const float ColumnWatchdog = 15f;

    private const float SweepWatchdog = 35f;

    /// <summary>What a contact leaves of the body's speed. <c>0.19999999</c> in the binary, which
    /// is 0.2 in float; the artefact is not transcribed. ⚠ Every component keeps its SIGN. The
    /// original reflects nothing, and reading it as a reflection is how debris ends up hovering.
    /// What lifts a body clear of the surface is the POSE correction, not the velocity.</summary>
    private const float Restitution = 0.2f;

    /// <summary>The two rest thresholds, asymmetric on purpose and tested per axis rather than on
    /// the horizontal magnitude. Above any of them a contact holds the body half a descending step
    /// clear of the surface; below all three it rests exactly on it.</summary>
    private const float RestHorizontal = 0.1f, RestVertical = 0.5f;

    /// <summary>Defensive only. The energy test ends a body after two or three contacts (each one
    /// takes a fifth of the speed), so this is never reached; it exists so a mistake in that test
    /// cannot spin a body forever on the hot path.</summary>
    private const int MaxContacts = 8;

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

    // forward_rotation: a live rate about the launch's own perpendicular. angle(t) = rate·t +
    // ½·accel·t², turned about `_tumbleAxis` — zero for a body that never drew a launch direction.
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

    /// <summary>The duration this body REPORTS: the authored <c>RUN_TIME</c>, or, for a launch that
    /// carries none, the time its own parabola takes to return to launch height. The caller reads it
    /// back rather than trusting the authored value, since only <see cref="Create"/> knows the
    /// randomised launch the solve rests on.
    ///
    /// <para>⚠ This is the SEQUENCE's number, not the body's. It is what the next null-start event
    /// waits on, so it is also what hides a vanish-shape piece (<c>BL-257</c>), and it sets the
    /// tumble rate, which is an angle divided by exactly this. What ends the body is
    /// <see cref="Finished"/>'s own ceiling, which for an untimed launch is the original's watchdog
    /// and can outlast this by seconds. Feeding the watchdog in here instead would leave every
    /// vanish-shape piece on screen for 15 s and slow 282 tumbles to a crawl.</para></summary>
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

    /// <summary>Whether this body turns at all: an authored rate AND an axis to turn it about. A
    /// vector-<c>translation</c> launch has the rate and no axis, which is the original's own
    /// arithmetic rather than a guard against it (see <see cref="TumbleAxis"/>).</summary>
    private bool Tumbles => (_tumbleRate != 0f || _tumbleAccel != 0f) && _tumbleAxis != Vector3.Zero;

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

        // The tier, decided once from the gravity block, in the original's own branch order
        // (FUN_004e8fa0: !DO_INTERSECTIONS, then !NO_ALTITUDE, then the column). The GRAVITY token
        // admits a body to the test at all, `do_intersections` upgrades it to the sweep, and
        // `no_altitude` opts out of the column only, so the order matters and not just the
        // conditions: the opt-out cannot suppress an explicitly authored sweep.
        //
        // A session that wires no mask selects neither tier and takes the untouched path, which is
        // the fallback made structural rather than remembered. The BOUNCE_SEQUENCE block rides
        // along because a contact-terminated body picks its branch from the surface it struck,
        // which is not knowable here.
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

        // Does this motion CONTINUE a contact landing — i.e. is it the `pNhit` settle hop a landing
        // dispatched onto the very node that just came to rest? One question, two consequences
        // below (no inherited momentum, no re-home to the authored rest), so it is asked once,
        // here, before either of them. The mark is one-shot and consuming it IS the answer. Read
        // `_hasBallistic`'s own condition off the data, since the field is not set until the
        // translation block below.
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
            // InheritedLocal is the plane's measured momentum rather than part of the authored
            // launch, and its magnitude carries its own WreckMomentum TUNE.
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
        // ⚠ The parabola's return to launch height is no longer what ENDS such a body — the
        // original's watchdog is, and before that its contact tier. What the solve still supplies
        // is the duration the SEQUENCE waits on and the tumble rate, which is why it stays: see
        // RunTime. A body with no apex (a shot-down zeppelin, a parachutist, the 8 of the vanish
        // shape the census names) reports 0 here and is admitted by its contact tier instead.
        //
        // Solved in the node's PARENT frame, the same frame the launch and gravity already live
        // in. Absent-RUN_TIME is read from the data, not from `runTime <= 0`, so an authored 0
        // keeps meaning zero.
        bool untimed = m._hasBallistic && data.Num("run_time") is null;
        if (untimed)
        {
            float flight = FlightToLaunchHeight(m._v0.Y, m._accel.Y);
            if (flight > 0f)
                m._runTime = flight;
        }

        // The termination ceiling: the authored RUN_TIME wherever there is one, which the original
        // applies universally rather than only to the flagged set. An untimed launch is bounded by
        // its tier's watchdog instead, so its clock ceiling is open and the watchdog (or a landing)
        // is what ends it. With no tier to back it — a session that wired no mask — it keeps the
        // solved parabola, the same structural fallback the tiers themselves take.
        //
        // ⚠ An open ceiling cannot leave a body running forever HERE, and the reason is the data,
        // not a cap: all 296 untimed ballistic events author a gravity block, so every one of them
        // descends, and a descending body either crosses a surface it can see (a landing) or finds
        // none and charges the watchdog. A future untimed event with gravity 0 would need one.
        m._watchdogLimit = untimed
            ? m._contactTier switch
            {
                MotionContactTier.Column => ColumnWatchdog,
                MotionContactTier.Sweep => SweepWatchdog,
                _ => 0f,
            }
            : 0f;
        m._ceiling = m._watchdogLimit > 0f ? float.PositiveInfinity : m._runTime;

        // A bounce is chosen from the surface struck, so it is armed at contact — or, on a watchdog
        // end, from a null surface, which is the `default` branch. Arming it here would beat both.
        // The exception is that same no-tier fallback: nothing there will ever reach a surface, so
        // the launch that names a bounce and solves its own flight still owes it up front.
        if (untimed && m._contactTier == MotionContactTier.None && m._runTime > 0f)
            m.PendingBounce = data.Obj("bounce_sequence")?.Str("default");

        // forward_rotation.Time is a RATE and its own acceleration, read straight across: the
        // original seeds a live rate at +0x84 from `initial` on the launch frame and integrates it
        // by `delta` every frame, so the run time never enters. ⚠ The clean multiples of π the
        // crash pieces carry (5π, 4.44π) are a coincidence of the authored numbers and were the
        // whole argument for the total-angle reading this replaces; they are not evidence.
        var fwd = data.Obj("forward_rotation")?.Obj("Time");
        m._tumbleRate = fwd?.Num("initial") ?? 0f;
        m._tumbleAccel = fwd?.Num("delta") ?? 0f;
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
        bool any = m._hasBallistic || m._hasScale || m.Tumbles || !m._spinRate.IsZeroApprox();
        return any ? m : null;
    }

    /// <summary>Advances the body one frame — and, for a contact-tested body, asks the world
    /// whether the step it is about to take ends under something.
    ///
    /// <para>⚠ Both tiers live HERE and never in <see cref="Seek"/>. <c>Seek</c> is also the
    /// pose/scrub entry point — <c>RESET_STATE</c> poses through it (<c>AnimRuntime</c>'s
    /// <c>Seek(0f)</c> calls) and AnimLab's timeline scrubs through it, in both directions — so a
    /// contact test there would fire on a backwards drag and land a piece that never flew.</para></summary>
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
        // A landed body freezes every channel at the contact moment: the piece is lying on the
        // ground, so it must not keep tumbling or scaling toward its authored end state.
        //
        // The ballistic clock stops at the CEILING and the channels are parameterised by the
        // REPORTED duration, which is the same number for every body that authors a RUN_TIME. They
        // part company on an untimed launch, where the body must keep flying to its landing while
        // its tumble keeps the rate its own parabola set.
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
            // The original ADDS its euler triple into the node's own angles and rebuilds the
            // matrix from them (FUN_004d25c0/FUN_004d1ba0 write node+0x18..0x20), so this composes
            // in euler rather than turning the live basis about an axis. The triple is
            // (axisX·angle, 0, axisZ·angle) — nothing is added to the Y angle at all.
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

    /// <summary>The axis one <c>forward_rotation</c> tumble turns about, as the euler triple the
    /// original accumulates: the horizontal perpendicular of the launch direction that
    /// <see cref="RangeLaunchDirection"/> just drew. <c>FUN_004e8fa0</c>'s <c>0x80</c> branch
    /// applies <c>( +0x78 · rate · dt , 0 , −( +0x70 · rate · dt ) )</c>, and <c>+0x70</c>/
    /// <c>+0x78</c> are that direction's cached X and Z — so the body pitches forward over its own
    /// throw, and the same authored rate reads differently per elevation.
    ///
    /// <para><b>⚠ Deliberately NOT unit length</b>, for the same reason the launch direction is not:
    /// its length is the launch's <c>h = 1 − |elev|/90</c>, so a near-vertical throw (h → 0) barely
    /// turns while a flat one (h → 1) turns at the full authored rate. Normalising it is what makes
    /// a steep launch tumble as fast as a flat one. Shared with <c>ProjectilePool</c>'s casing
    /// ejection, which flies the same <c>gunshell</c> event and must not spell this twice.</para>
    ///
    /// <para>⚠ A zero vector in means no tumble, and that is a DECODE, not a guard: the vector
    /// <c>translation</c> form never writes the cache, and the parser zeroes the event struct
    /// before parsing (<c>005085e0</c>), so those 495 events multiply a live rate by nothing.
    /// </para></summary>
    internal static Vector3 TumbleAxis(Vector3 launchDir) => new(launchDir.Z, 0f, -launchDir.X);

    /// <summary>How far a tumble has turned at <paramref name="t"/>: the integral of a rate that is
    /// itself integrating its own <c>delta</c> (<c>+0x84 += dt · +0x80</c> every frame), in closed
    /// form. The discrete sum and this differ by one frame's worth of the acceleration, which is
    /// nothing on the 2 events install-wide that author a non-zero <c>delta</c>.</summary>
    internal static float TumbleAngle(float rate, float accel, float t) => (rate + 0.5f * accel * t) * t;

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

    /// <summary>The default contact tier, the ground column. Looks straight down from where the
    /// body is and ends the flight once the step it is about to take would put it under whatever
    /// is there. Returns whether contact ended the body.
    ///
    /// <para>Decoded from <c>FUN_004e8fa0</c>'s <c>!DO_INTERSECTIONS</c>, <c>!NO_ALTITUDE</c>
    /// branch, via <c>FUN_004e9e30</c> and <c>FUN_004c76e0</c>. The query is not a trajectory test:
    /// <c>FUN_004c76e0</c> floors the body's own <c>(x, z)</c> into the world database's terrain
    /// grid cell and collects that cell's surface records, so it answers "what is in this column"
    /// rather than "what did the path cross". That is the whole difference between the two tiers,
    /// and it is why a piece can pass over a ledge between frames. The landing test is on the next
    /// point, <c>y + stepY &lt; surfaceHeight</c>.</para>
    ///
    /// <para>⚠ Two deliberate departures from that transcription. (1) <c>FUN_004e9e30</c> picks the
    /// cell record whose height is nearest the body's y in ABSOLUTE value, rejecting only
    /// replacements more than 10 m above it, and takes the first record unconditionally whatever
    /// its height. A surface above the body can therefore win, and the landing test then lifts the
    /// body onto it. This casts downward and takes the first surface under the body, which is what
    /// that pick degenerates to whenever the cell holds one ground surface. (2) The original's
    /// column reads the node flags <c>altitude_surface</c> AND <c>intersect_surface</c> together;
    /// this engine builds colliders from <c>intersect_surface</c> alone. The two sets differ by 28
    /// nodes install-wide (14 in C1, 0 in C1B/C1C/C2B, 2 to 4 elsewhere), every one of them a
    /// destructible's own sub-part rather than terrain or a building shell.</para>
    ///
    /// <para>⚠ No <see cref="ArmDistance"/>/<see cref="ArmSeconds"/> epsilon, unlike the sweep. The
    /// original arms this tier on the descending step alone, and a launch climbs before it falls,
    /// so it cannot contact the thing it left on its first frame.</para></summary>
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
        // A3's rule: a descending step admits the test, and `complex` widens it to every step.
        // ⚠ Here it saves the query and decides nothing, because a downward column reports only
        // surfaces at or below `from`, so a rising step can never satisfy the landing test. The
        // original needs the rule because its cell query can return a surface above the body and
        // because its sign is the parent-frame one, which the COMPLEX form makes meaningless. This
        // sign is the world's, which is the true answer for both forms.
        if (!_complexGravity && to.Y >= from.Y)
            return false;

        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            from, from + Vector3.Down * ColumnDepth, _contactMask));
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

    /// <summary>Comes to rest, the shared response both tiers end on. The two mechanisms differ in
    /// what they ask the world, never in what they do with the answer, so a column landing and a
    /// sweep landing cannot disagree about where the piece ends up or which sequence it owes.
    ///
    /// <para>The landing is the whole meaning of a bounce-terminated flight ending, and for that
    /// family the branch could not be chosen until now, since the struck body is what picks it.
    /// <see cref="MotionSet.Tick"/> turns this into the dispatched <c>Landing</c>.</para></summary>
    /// <summary>The original's watchdog, charged by a contact query that ran and found nothing.
    /// Returns whether it just ended the body.
    ///
    /// <para>⚠ Only an EMPTY query charges it. A body descending toward ground it can see never
    /// accumulates a tick of this, and neither does one climbing (the column is not consulted on a
    /// rising step at all), so it is not a flight timer: it is how long a body has been falling
    /// past nothing. It also only bounds a launch that authors no <c>RUN_TIME</c> — with one, the
    /// ceiling is that instead and this never runs.</para>
    ///
    /// <para>The body ends where it is, and owes its <c>default</c> branch: the original runs the
    /// bounce block on a watchdog end too, with a null surface record, which indexes to
    /// <c>default</c>.</para></summary>
    private bool Watchdog(float dt, float next)
    {
        if (_watchdogLimit <= 0f)
            return false;
        _watchdog += dt;
        return _watchdog >= _watchdogLimit
               && Land(next, BallisticOrigin(next), null, byContact: false);
    }

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

    /// <summary>World up, in the node's parent frame — the direction the pose correction lifts
    /// along. The original adds to its step's own Y, which is the same thing wherever the parent is
    /// world-aligned and is what it means everywhere else.</summary>
    private Vector3 ParentUp()
    {
        var parent = (Target.GetParent() as Node3D)?.GlobalTransform.Basis;
        return parent is { } b ? b.Inverse() * Vector3.Up : Vector3.Up;
    }

    /// <summary>The ballistic origin at time <paramref name="t"/>, in the node's parent frame —
    /// the same closed-form solve <see cref="Seek"/> poses with, factored out because the sweep
    /// needs both ends of the step it is about to take. Never consulted for a landed body.
    ///
    /// <para>Measured from <see cref="_ballisticStart"/> rather than from 0, because a contact the
    /// body survives re-bases the launch at the surface with a fifth of its speed.</para></summary>
    private Vector3 BallisticOrigin(float t)
    {
        float e = t - _ballisticStart;
        return _heldOrigin + _v0 * e + 0.5f * e * e * _accel;
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
