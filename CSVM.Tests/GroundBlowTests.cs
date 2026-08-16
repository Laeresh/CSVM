using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Ground blow: the original biases the player's control response away from anything large the
/// nose is closing on. Decode, the closed-form law and its constants: docs/org/flightModel.md's
/// "Ground blow" section. Everything here is asserted as the difference between two otherwise
/// identical steps, one fed a probe hit and one not, which needs no knowledge of the stick, the
/// bank coupling, the weathervane or any tuning constant.
/// </summary>
public class GroundBlowTests
{
    private const float Dt = 1f / 60f;
    private const float Elev = 400f;   // groundblow_elev, m — this install's authored value
    private const float Mag = 10f;     // groundblow_mag — likewise
    private const float IntoFactor = 0.05f;

    // AngMomentumDamp on Bhawk() below — needed only for the AI-law tests because, unlike the
    // player law, the AI push does not read cmd, so the free dt·damp cancellation the player tests
    // exploit (baseline.Dot(v), itself already dt·damp-scaled, supplying the addition's own missing
    // factor) does not apply: the AI addition needs its dt·damp spelled out explicitly against a
    // no-hit BASELINE.
    private const float AngDamp = 5f;

    // A slope ahead and below, its normal facing up and back at the aircraft — the cliff
    // case, in BODY coordinates (nose −Z, up +Y, right +X). Its escape axis is body +X, i.e. the
    // bias is pitch-up.
    private static readonly Vector3 SlopeNormalBody = new Vector3(0f, 1f, 1f).Normalized();

    [Fact]
    public void AMissAddsNothing()
    {
        // The zero normal is the only "no hit" signal, matching the original's zero-initialised
        // output vector: a miss, a back-facing surface and a filtered emitter all reach the law as
        // this and must all be inert.
        var baseline = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 10f);
        Assert.True((probed - baseline).Length() < 1e-9f,
            $"no hit must change nothing: {probed} vs {baseline}");
    }

    [Fact]
    public void ASurfaceFacingAwayAddsNothing()
    {
        // dot(b, n) > 0 strictly: the surface has to face back AT you. A wall whose normal points
        // the same way you are flying is one you are leaving, and it must not push.
        var baseline = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(Basis.Identity, pitch: 1f, normal: new Vector3(0f, 1f, -1f).Normalized(), dist: 0f);
        Assert.True((probed - baseline).Length() < 1e-9f,
            $"a surface facing away must not repel: {probed} vs {baseline}");
    }

    [Fact]
    public void ADeadOnApproachGetsNoHelpAtAll()
    {
        // n → b collapses the escape axis, and that IS the original's "ground blow never saves a
        // head-on collision". A renormalised or special-cased degenerate axis would produce the
        // strongest possible push exactly where the original produces none.
        var baseline = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Back, dist: 0f);
        Assert.True((probed - baseline).Length() < 1e-9f,
            $"a dead-on approach must get nothing: {probed} vs {baseline}");
    }

    [Theory]
    [InlineData(0f)]      // at contact: S = sqrt(c), the strongest the bias ever gets
    [InlineData(100f)]    // a quarter of the way out
    [InlineData(300f)]    // three quarters
    public void CommandingAwayIsAmplifiedByTheAuthoredMagnitude(float dist)
    {
        var baseline = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(Basis.Identity, pitch: 1f, normal: SlopeNormalBody, dist: dist);
        var v = ExpectedAxis(dist);

        // The pull is UP and the slope is below: the command already points along +V, so it is
        // amplified — by 1 + mag·S² along that axis, 11× at contact with the authored 10.
        AssertMatches(baseline + v * (baseline.Dot(v) * Mag), probed, dist);
        Assert.True(probed.X > baseline.X,
            $"a pull away from the slope must be amplified: {probed.X:0.000000} vs {baseline.X:0.000000}");
    }

    [Fact]
    public void CommandingIntoIsCutAndNeverReversed()
    {
        // 0.05 × 10 = 0.5 at contact: the offending rotation is halved, and the original cannot
        // take the stick off the pilot. A push that came back reversed would be the wrong feature
        // entirely — an autopilot, not a bias.
        var baseline = OneStep(Basis.Identity, pitch: -1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(Basis.Identity, pitch: -1f, normal: SlopeNormalBody, dist: 0f);
        var v = ExpectedAxis(0f);

        AssertMatches(baseline + v * (IntoFactor * Mathf.Abs(baseline.Dot(v)) * Mag), probed, 0f);
        Assert.True(probed.X < 0f,
            $"a push into the slope must survive, weakened: {probed.X:0.000000} must stay negative");
        Assert.True(probed.X > baseline.X,
            $"and must be weaker than unaided: {probed.X:0.000000} vs {baseline.X:0.000000}");
    }

    [Fact]
    public void TheFalloffIsQuadraticInProximityRatherThanLinear()
    {
        // The single tell that separates the decoded law from the plausible one-power reading: the
        // scaled axis is used TWICE, once in the dot and once in the add. At half the ray's length
        // the amplification is 1 + mag·S² with S = 0.5·sqrt(c), not 1 + mag·S.
        var baseline = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(Basis.Identity, pitch: 1f, normal: SlopeNormalBody, dist: Elev / 2f);
        float s = ExpectedProximity(Elev / 2f);

        float quadratic = baseline.X * (1f + Mag * s * s);
        float linear = baseline.X * (1f + Mag * s);
        Assert.True(Mathf.IsEqualApprox(probed.X, quadratic, 1e-6f),
            $"half way out the gain is 1 + 10·S² = {quadratic:0.000000}, not 1 + 10·S = {linear:0.000000} "
            + $"(got {probed.X:0.000000}, S = {s:0.0000})");
    }

    [Fact]
    public void TheProbeReachIsTheAuthoredRayLengthInMetres()
    {
        // 400 is a LENGTH in metres and the falloff's own denominator, not a trigger altitude and
        // not feet. At the ray's end the term is identically zero and nothing steps.
        var baseline = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var atEnd = OneStep(Basis.Identity, pitch: 1f, normal: SlopeNormalBody, dist: Elev);
        var justInside = OneStep(Basis.Identity, pitch: 1f, normal: SlopeNormalBody, dist: Elev - 1f);

        Assert.True((atEnd - baseline).Length() < 1e-9f,
            $"at {Elev} m the ramp has run out: {atEnd} vs {baseline}");
        Assert.True(justInside.X > baseline.X,
            $"one metre inside it has not: {justInside.X:0.000000} vs {baseline.X:0.000000}");
    }

    [Fact]
    public void TheEscapeAxisIsBodyFrameNotWorldFrame()
    {
        // A world-frame axis mistake is invisible wings-level (the two frames agree there), so
        // this must not check wings-level.
        var attitude = Basis.Identity.Rotated(Vector3.Back, Mathf.DegToRad(90f));
        var baseline = OneStep(attitude, pitch: 1f, normal: Vector3.Zero, dist: 0f);
        var probed = OneStep(attitude, pitch: 1f, normal: attitude * SlopeNormalBody, dist: 0f);
        var v = ExpectedAxis(0f);   // body-frame, so unchanged by the roll

        AssertMatches(baseline + v * (baseline.Dot(v) * Mag), probed, 0f);
    }

    [Fact]
    public void TheVelocitySteerRidesAlongAtTwiceProximity()
    {
        // Stick centred, path offset in yaw: the escape axis is body pitch, so the torque is
        // identically zero and the two runs' attitudes stay bit-identical, leaving only the
        // chases' decay-factor ratio.
        float alone = GapAfter(pitch: 0f, normal: Vector3.Zero);
        float away = GapAfter(pitch: 0f, normal: SlopeNormalBody);
        float expected = alone * Mathf.Exp(-2f * ExpectedProximity(0f) * Dt);

        Assert.True(Mathf.IsEqualApprox(away, expected, 1e-6f),
            $"the leftover gap must be {expected:0.000000} rad (alone {alone:0.000000} × exp(−2·S·dt)), "
            + $"got {away:0.000000}");
    }

    [Fact]
    public void TheVelocitySteerStopsWhenCommandingIntoTheSurface()
    {
        // The original zeroes proximity on the into-obstacle branch, so the steer runs on nothing;
        // the torques still differ, but only in pitch, moving this yaw gap at second order.
        float intoAlone = GapAfter(pitch: -1f, normal: Vector3.Zero);
        float into = GapAfter(pitch: -1f, normal: SlopeNormalBody);
        float ifItRanAnyway = intoAlone * Mathf.Exp(-2f * ExpectedProximity(0f) * Dt);

        Assert.True(Mathf.IsEqualApprox(into, intoAlone, 1e-4f),
            $"commanding into the surface must leave the path chase alone: {into:0.000000} vs "
            + $"{intoAlone:0.000000} (an unsuppressed steer would give {ifItRanAnyway:0.000000})");
    }

    [Fact]
    public void TheAiLawIsAFixedPushNotACommandProportionalOne()
    {
        // The un-cut authored value (docs/org/flightModel.md's "Ground blow"); the 2.5 s
        // post-carrier-drop cut is a separate, unmodelled factor. The push does not read cmd, so
        // a deflected and a centred stick get the same bias relative to their own baseline.
        const float aiGroundBlow = 0.5f;
        var deflectedBase = OneStep(Basis.Identity, pitch: 1f, normal: Vector3.Zero, dist: 0f, ai: true);
        var deflectedHit = OneStep(Basis.Identity, pitch: 1f, normal: SlopeNormalBody, dist: 0f, ai: true);
        var centredBase = OneStep(Basis.Identity, pitch: 0f, normal: Vector3.Zero, dist: 0f, ai: true);
        var centredHit = OneStep(Basis.Identity, pitch: 0f, normal: SlopeNormalBody, dist: 0f, ai: true);
        var v = ExpectedAxis(0f);
        var expected = ExpectedAiDelta(v, aiGroundBlow);

        AssertMatches(deflectedBase + expected, deflectedHit, 0f);
        AssertMatches(centredBase + expected, centredHit, 0f);
    }

    [Fact]
    public void TheAiLawIsNeverSuppressedByCommandingIntoTheSurface()
    {
        // The player law halves an into-obstacle command; the AI law has no such branch at all —
        // "independent of the AI's own command" applies to sign as much as magnitude.
        const float aiGroundBlow = 0.5f;
        var pushingInBase = OneStep(Basis.Identity, pitch: -1f, normal: Vector3.Zero, dist: 0f, ai: true);
        var pushingInHit = OneStep(Basis.Identity, pitch: -1f, normal: SlopeNormalBody, dist: 0f, ai: true);
        var v = ExpectedAxis(0f);
        AssertMatches(pushingInBase + ExpectedAiDelta(v, aiGroundBlow), pushingInHit, 0f);
    }

    [Fact]
    public void TheAiFalloffIsLinearInProximityRatherThanQuadratic()
    {
        // The player law's escape axis carries S once and is then dotted with the command for a
        // second factor of S (the quadratic falloff GroundBlowTests already pins). The AI law never
        // dots into cmd, so its only S comes from the escape axis itself — linear, not quadratic.
        const float aiGroundBlow = 0.5f;
        var baseline = OneStep(Basis.Identity, pitch: 0f, normal: Vector3.Zero, dist: 0f, ai: true);
        var atHalf = OneStep(Basis.Identity, pitch: 0f, normal: SlopeNormalBody, dist: Elev / 2f, ai: true);
        float s = ExpectedProximity(Elev / 2f);
        var linear = baseline + ExpectedAiDelta(new Vector3(s, 0f, 0f), aiGroundBlow);
        var quadraticWouldBe = baseline + ExpectedAiDelta(new Vector3(s * s, 0f, 0f), aiGroundBlow);

        Assert.True(Mathf.IsEqualApprox(atHalf.X, linear.X, 1e-6f),
            $"half way out the AI push must be linear in S = {linear.X:0.000000}, not quadratic "
            + $"{quadraticWouldBe.X:0.000000} (got {atHalf.X:0.000000})");
    }

    [Fact]
    public void TheAiVelocitySteerNeverSuppresses()
    {
        // The player path zeroes its steer while commanding into the obstacle (S is zeroed on that
        // branch, TheVelocitySteerStopsWhenCommandingIntoTheSurface pins it); the AI path has no
        // command-direction branch to zero it, so the steer always rides at 2·S regardless of pitch.
        float alone = GapAfter(pitch: 0f, normal: Vector3.Zero, ai: true);
        float pushingIn = GapAfter(pitch: -1f, normal: SlopeNormalBody, ai: true);
        float expected = alone * Mathf.Exp(-2f * ExpectedProximity(0f) * Dt);

        Assert.True(Mathf.IsEqualApprox(pushingIn, expected, 1e-6f),
            $"the AI steer must run even while commanding into the surface: expected {expected:0.000000} "
            + $"rad, got {pushingIn:0.000000}");
    }

    // The escape axis the law should build for SlopeNormalBody, in body
    // coordinates and already scaled by proximity — `normalize(n × b) · S`, which for this
    // slope is body +X (pitch up).
    private static Vector3 ExpectedAxis(float dist) => new(ExpectedProximity(dist), 0f, 0f);

    // The AI law's contribution to one step's `BodyRates`: `v · (ai_groundblow ·
    // groundblow_mag)` is a command-accumulator torque like the player law's, carrying no dt of
    // its own — the same single `dt` then `exp(−dt·damp)` the caller applies to the whole
    // accumulator applies here too.
    private static Vector3 ExpectedAiDelta(Vector3 v, float aiGroundBlow) =>
        v * (aiGroundBlow * Mag) * Dt * Mathf.Exp(-Dt * AngDamp);

    private static float ExpectedProximity(float dist) =>
        Mathf.Sqrt(Vector3.Back.Dot(SlopeNormalBody)) * (Elev - dist) / Elev;

    private static void AssertMatches(Vector3 expected, Vector3 actual, float dist)
    {
        Assert.True((actual - expected).Length() < 1e-6f,
            $"at {dist:0} m the rates must be {expected} (baseline + V·dot(baseline, V)·{Mag}), got {actual}");
    }

    // The angle in radians between the flight path and the nose after one step, with the
    // path started 10° off the nose IN YAW so there is a gap for the steer to close on an axis the
    // pitch-axis bias does not move.
    private static float GapAfter(float pitch, Vector3 normal, bool ai = false)
    {
        var m = Fresh(ai: ai);
        m.VelocityDir = (Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(10f)) * m.VelocityDir).Normalized();
        m.Step(Input(pitch, normal, 0f), Dt);
        return m.VelocityDir.AngleTo(-m.Attitude.Z);
    }

    private static Vector3 OneStep(Basis attitude, float pitch, Vector3 normal, float dist, bool ai = false)
    {
        var m = Fresh(attitude, ai);
        m.Step(Input(pitch, normal, dist), Dt);
        return m.BodyRates;
    }

    private static FlightInput Input(float pitch, Vector3 normal, float dist) => new()
    {
        Pitch = pitch,
        Throttle = 1f,
        GroundBlowNormal = normal,
        GroundBlowDistM = dist,
    };

    private static FlightModel Fresh(Basis? attitude = null, bool ai = false)
    {
        var m = new FlightModel(Bhawk(), ai);
        m.Reset(Vector3.Zero, attitude ?? Basis.Identity, 120f, 1f);
        return m;
    }

    // The Bloodhawk's real dynamics, with this install's authored ground-blow values
    // rather than the executable's 100/1.5/0.9 fallbacks (PlaneStatsFlightGlobalsTests pins the
    // read itself). `AiGroundBlow` at the authored 0.5 for the AI-path tests.
    private static PlaneStats Bhawk() => new()
    {
        PitchTorque = 3.3f,
        RollTorque = 7.5f,
        RudderTorque = 2f,
        ReturnRate = 3f,
        AngMomentumDamp = 5f,
        RecInertia = new Vector3(1.18f, 1f, 1.1f),
        FdSpeed = 135f,
        VehWeight = 1900f,
        RefArea = 330f,
        DragFactor = 0.37f,
        GroundBlowElev = Elev,
        GroundBlowMag = Mag,
        AiGroundBlow = 0.5f,
    };
}
